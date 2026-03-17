import os
import torch
import torch.nn.functional as F
import numpy as np
import folder_paths

from .utils import processing as processing_utils
from .utils import loading as loading_utils
from .filmgrainer import filmgrainer

dir_luts = folder_paths.folder_names_and_paths.get("luts", None)
existing_list = dir_luts[0]
folder_paths.folder_names_and_paths["luts"] = (
    existing_list + [os.path.join(folder_paths.models_dir, "luts")], set(['.cube']))

# LUT file cache -- avoids re-reading .cube files from disk on every invocation
_lut_file_cache: dict = {}
# Device LUT tensor cache -- avoids re-uploading the same LUT to GPU every invocation
_lut_tensor_cache: dict = {}


def _load_lut(lut_path: str):
    if lut_path not in _lut_file_cache:
        _lut_file_cache[lut_path] = loading_utils.read_lut(lut_path, clip=True)
    return _lut_file_cache[lut_path]


def _lut_tensor(lut_path: str, table: np.ndarray, device: torch.device,
                dtype: torch.dtype = torch.float32) -> torch.Tensor:
    key = (lut_path, str(device), str(dtype))
    if key not in _lut_tensor_cache:
        _lut_tensor_cache[key] = torch.from_numpy(table).to(device=device, dtype=dtype)
    return _lut_tensor_cache[key]


def _apply_3d_lut(image: torch.Tensor, lut_t: torch.Tensor) -> torch.Tensor:
    """
    Torch trilinear interpolation for 3D LUTs.
    image: [..., 3] float32 in [0, 1]
    lut_t: [N, N, N, 3] torch tensor
    """
    N = lut_t.shape[0]

    coords = (image * (N - 1)).clamp(0, N - 1)
    c0 = coords.floor().long().clamp(0, N - 2)
    c1 = (c0 + 1).clamp(0, N - 1)
    w = (coords - c0.float()).clamp(0.0, 1.0)

    r0, g0, b0 = c0[..., 0], c0[..., 1], c0[..., 2]
    r1, g1, b1 = c1[..., 0], c1[..., 1], c1[..., 2]
    wr, wg, wb = w[..., 0:1], w[..., 1:2], w[..., 2:3]

    c000 = lut_t[r0, g0, b0]; c100 = lut_t[r1, g0, b0]
    c010 = lut_t[r0, g1, b0]; c110 = lut_t[r1, g1, b0]
    c001 = lut_t[r0, g0, b1]; c101 = lut_t[r1, g0, b1]
    c011 = lut_t[r0, g1, b1]; c111 = lut_t[r1, g1, b1]

    c00 = c000 + wr * (c100 - c000); c01 = c001 + wr * (c101 - c001)
    c10 = c010 + wr * (c110 - c010); c11 = c011 + wr * (c111 - c011)
    c0_ = c00 + wg * (c10 - c00);    c1_ = c01 + wg * (c11 - c01)
    return (c0_ + wb * (c1_ - c0_)).clamp(0.0, 1.0)


def _apply_1d_lut(image: torch.Tensor, lut_t: torch.Tensor) -> torch.Tensor:
    """Per-channel linear interpolation for LUT3x1D."""
    N = lut_t.shape[0]

    coords = (image * (N - 1)).clamp(0, N - 1)
    c0 = coords.floor().long().clamp(0, N - 2)
    c1 = (c0 + 1).clamp(0, N - 1)
    w = (coords - c0.float()).clamp(0.0, 1.0)

    result = torch.stack([
        lut_t[c0[..., ch], ch] + w[..., ch] * (lut_t[c1[..., ch], ch] - lut_t[c0[..., ch], ch])
        for ch in range(3)
    ], dim=-1)
    return result.clamp(0.0, 1.0)


class ProPostVignette:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "image": ("IMAGE",),
                "intensity": ("FLOAT", {
                    "default": 1.0,
                    "min": 0.0,
                    "max": 10.0,
                    "step": 0.01
                }),
                "center_x": ("FLOAT", {
                    "default": 0.5,
                    "min": 0.0,
                    "max": 1.0,
                    "step": 0.01
                }),
                "center_y": ("FLOAT", {
                    "default": 0.5,
                    "min": 0.0,
                    "max": 1.0,
                    "step": 0.01
                }),
            },
        }

    RETURN_TYPES = ("IMAGE",)
    RETURN_NAMES = ()

    FUNCTION = "vignette_image"

    CATEGORY = "Pro Post/Camera Effects"

    @torch.inference_mode()
    def vignette_image(self, image: torch.Tensor, intensity: float,
                       center_x: float, center_y: float):
        if intensity == 0:
            return (image,)

        B, H, W, _ = image.shape
        device = image.device

        x = torch.linspace(-1, 1, W, device=device) - (2 * center_x - 1)
        y = torch.linspace(-1, 1, H, device=device) - (2 * center_y - 1)
        Y, X = torch.meshgrid(y, x, indexing='ij')

        max_dist = max(
            (center_x ** 2 + center_y ** 2) ** 0.5,
            ((1 - center_x) ** 2 + center_y ** 2) ** 0.5,
            (center_x ** 2 + (1 - center_y) ** 2) ** 0.5,
            ((1 - center_x) ** 2 + (1 - center_y) ** 2) ** 0.5,
        )
        radius = (X ** 2 + Y ** 2).sqrt() / (max_dist * 2 ** 0.5)
        opacity = min(intensity, 1.0)
        vignette = (1.0 - radius * opacity).clamp(0.0, 1.0)
        vignette = vignette.unsqueeze(0).unsqueeze(-1)

        return ((image * vignette).clamp(0.0, 1.0),)


class ProPostFilmGrain:
    grain_types = ["Fine", "Fine Simple", "Coarse", "Coarser"]

    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "image": ("IMAGE",),
                "gray_scale": ("BOOLEAN", {
                    "default": False
                }),
                "grain_type": (s.grain_types,),
                "grain_sat": ("FLOAT", {
                    "default": 0.5,
                    "min": 0.0,
                    "max": 1.0,
                    "step": 0.01
                }),
                "grain_power": ("FLOAT", {
                    "default": 0.7,
                    "min": 0.0,
                    "max": 1.0,
                    "step": 0.01
                }),
                "shadows": ("FLOAT", {
                    "default": 0.2,
                    "min": 0.0,
                    "max": 1.0,
                    "step": 0.01
                }),
                "highs": ("FLOAT", {
                    "default": 0.2,
                    "min": 0.0,
                    "max": 1.0,
                    "step": 0.01
                }),
                "scale": ("FLOAT", {
                    "default": 1.0,
                    "min": 0.0,
                    "max": 10.0,
                    "step": 0.01
                }),
                "sharpen": ("INT", {
                    "default": 0,
                    "min": 0,
                    "max": 10
                }),
                "src_gamma": ("FLOAT", {
                    "default": 1.0,
                    "min": 0.0,
                    "max": 10.0,
                    "step": 0.01
                }),
                "seed": ("INT", {
                    "default": 1,
                    "min": 1,
                    "max": 1000
                }),
            },
        }

    RETURN_TYPES = ("IMAGE",)
    RETURN_NAMES = ()

    FUNCTION = "filmgrain_image"

    CATEGORY = "Pro Post/Camera Effects"

    @torch.inference_mode()
    def filmgrain_image(self, image: torch.Tensor, gray_scale: bool, grain_type: str,
                        grain_sat: float, grain_power: float, shadows: float,
                        highs: float, scale: float, sharpen: int,
                        src_gamma: float, seed: int):
        grain_type_index = self.grain_types.index(grain_type) + 1
        results = []
        for b in range(image.shape[0]):
            result = filmgrainer.process(
                image[b], scale, src_gamma, grain_power, shadows, highs,
                grain_type_index, grain_sat, gray_scale, sharpen, seed + b
            )
            results.append(result)
        return (torch.stack(results),)


class ProPostRadialBlur:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "image": ("IMAGE",),
                "blur_strength": ("FLOAT", {
                    "default": 64.0,
                    "min": 0.0,
                    "max": 256.0,
                    "step": 1.0
                }),
                "center_x": ("FLOAT", {
                    "default": 0.5,
                    "min": 0.0,
                    "max": 1.0,
                    "step": 0.01
                }),
                "center_y": ("FLOAT", {
                    "default": 0.5,
                    "min": 0.0,
                    "max": 1.0,
                    "step": 0.01
                }),
                "focus_spread": ("FLOAT", {
                    "default": 1,
                    "min": 0.1,
                    "max": 8.0,
                    "step": 0.1
                }),
                "steps": ("INT", {
                    "default": 5,
                    "min": 1,
                    "max": 32,
                }),
            },
        }

    RETURN_TYPES = ("IMAGE",)
    RETURN_NAMES = ()

    FUNCTION = "radialblur_image"

    CATEGORY = "Pro Post/Blur Effects"

    @torch.inference_mode()
    def radialblur_image(self, image: torch.Tensor, blur_strength: float,
                         center_x: float, center_y: float,
                         focus_spread: float, steps: int):
        B, H, W, C = image.shape
        device = image.device

        cx, cy = W * center_x, H * center_y
        xs = torch.arange(W, dtype=torch.float32, device=device) - cx
        ys = torch.arange(H, dtype=torch.float32, device=device) - cy
        Y, X = torch.meshgrid(ys, xs, indexing='ij')
        max_dist = max(
            (cx ** 2 + cy ** 2) ** 0.5,
            ((W - cx) ** 2 + cy ** 2) ** 0.5,
            (cx ** 2 + (H - cy) ** 2) ** 0.5,
            ((W - cx) ** 2 + (H - cy) ** 2) ** 0.5,
        )
        radial_mask = ((X ** 2 + Y ** 2).sqrt() / max_dist).clamp(0.0, 1.0)

        img_4d = image.permute(0, 3, 1, 2)
        blurred = processing_utils.generate_blurred_images(
            img_4d, blur_strength, steps, focus_spread)
        out = processing_utils.apply_blurred_images(img_4d, blurred, radial_mask)
        return (out.permute(0, 2, 3, 1).clamp(0.0, 1.0),)


class ProPostDepthMapBlur:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "image": ("IMAGE",),
                "depth_map": ("IMAGE",),
                "blur_strength": ("FLOAT", {
                    "default": 64.0,
                    "min": 0.0,
                    "max": 256.0,
                    "step": 1.0
                }),
                "focal_depth": ("FLOAT", {
                    "default": 1.0,
                    "min": 0.0,
                    "max": 1.0,
                    "step": 0.01
                }),
                "focus_spread": ("FLOAT", {
                    "default": 1,
                    "min": 1.0,
                    "max": 8.0,
                    "step": 0.1
                }),
                "steps": ("INT", {
                    "default": 5,
                    "min": 1,
                    "max": 32,
                }),
                "focal_range": ("FLOAT", {
                    "default": 0.0,
                    "min": 0.0,
                    "max": 1.0,
                    "step": 0.01
                }),
                "mask_blur": ("INT", {
                    "default": 1,
                    "min": 1,
                    "max": 127,
                    "step": 2
                }),
            },
        }

    RETURN_TYPES = ("IMAGE", "MASK")
    RETURN_NAMES = ()

    FUNCTION = "depthblur_image"
    DESCRIPTION = """
    blur_strength: Represents the blur strength. This parameter controls the overall intensity of the blur effect; the higher the value, the more blurred the image becomes.

    focal_depth: Represents the focal depth. This parameter is used to determine which depth level in the image should remain sharp, while other levels are blurred based on depth differences.

    focus_spread: Represents the focus spread range. This parameter controls the size of the blur transition area near the focal depth; the larger the value, the wider the transition area, and the smoother the blur effect spreads around the focus.

    steps: Represents the number of steps in the blur process. This parameter determines the calculation precision of the blur effect; the more steps, the finer the blur effect, but this also increases the computational load.

    focal_range: Represents the focal range. This parameter is used to adjust the depth range within the focal depth that remains sharp; the larger the value, the wider the area around the focal depth that remains sharp.

    mask_blur: Represents the mask blur strength for blurring the depth map. This parameter controls the intensity of the depth map's blur treatment, used for preprocessing the depth map before calculating the final blur effect, to achieve a more natural blur transition.
    """

    CATEGORY = "Pro Post/Blur Effects"

    @torch.inference_mode()
    def depthblur_image(self, image: torch.Tensor, depth_map: torch.Tensor,
                        blur_strength: float, focal_depth: float,
                        focus_spread: float, steps: int,
                        focal_range: float, mask_blur: int):
        B, H, W, C = image.shape
        device = image.device

        img_4d = image.permute(0, 3, 1, 2)
        depth_4d = depth_map.permute(0, 3, 1, 2)
        if depth_4d.shape[2:] != (H, W):
            depth_4d = F.interpolate(
                depth_4d.float(), size=(H, W), mode='bilinear', align_corners=False)

        if depth_4d.shape[1] >= 3:
            luma_w = torch.tensor([0.2126, 0.7152, 0.0722], device=device).view(1, 3, 1, 1)
            depth_gray = (depth_4d[:, :3].float() * luma_w).sum(dim=1, keepdim=True)
        else:
            depth_gray = depth_4d[:, :1].float()

        depth_mask = (depth_gray - focal_depth).abs()
        mask_max = depth_mask.amax(dim=(2, 3), keepdim=True).clamp(min=1e-7)
        depth_mask = (depth_mask / mask_max).clamp(0.0, 1.0)

        depth_mask = torch.where(
            depth_mask < focal_range,
            torch.zeros_like(depth_mask),
            (depth_mask - focal_range) / (1.0 - focal_range + 1e-7)
        ).clamp(0.0, 1.0)

        if mask_blur > 1:
            k = mask_blur if mask_blur % 2 == 1 else mask_blur + 1
            depth_mask = processing_utils.gaussian_blur(depth_mask, k).clamp(0.0, 1.0)

        blurred = processing_utils.generate_blurred_images(
            img_4d, blur_strength, steps, focus_spread)
        out = processing_utils.apply_blurred_images(img_4d, blurred, depth_mask)

        out_images = out.permute(0, 2, 3, 1).clamp(0.0, 1.0)
        out_masks = depth_mask.squeeze(1)
        return (out_images, out_masks)


class ProPostApplyLUT:
    @classmethod
    def INPUT_TYPES(s):
        return {
            "required": {
                "image": ("IMAGE",),
                "lut_name": (folder_paths.get_filename_list("luts"),),
                "strength": ("FLOAT", {
                    "default": 1.0,
                    "min": 0.0,
                    "max": 1.0,
                    "step": 0.01
                }),
                "log": ("BOOLEAN", {
                    "default": False
                }),
            },
        }

    RETURN_TYPES = ("IMAGE",)
    RETURN_NAMES = ()

    FUNCTION = "lut_image"

    CATEGORY = "Pro Post/Color Grading"

    @torch.inference_mode()
    def lut_image(self, image: torch.Tensor, lut_name, strength: float, log: bool):
        if strength == 0:
            return (image,)

        lut_path = os.path.join(existing_list[0], lut_name)
        lut = _load_lut(lut_path)
        device = image.device
        lut_t = _lut_tensor(lut_path, lut.table, device)

        default_domain = np.array([[0., 0., 0.], [1., 1., 1.]])
        is_non_default = not np.array_equal(lut.domain, default_domain)

        if is_non_default:
            dom_scale = torch.from_numpy(
                (lut.domain[1] - lut.domain[0]).copy()).to(device).float()
            dom_min = torch.from_numpy(lut.domain[0].copy()).to(device).float()

        frame = image.clone()

        if is_non_default:
            frame = frame * dom_scale + dom_min
        if log:
            frame = frame.clamp(min=1e-7).pow(1.0 / 2.2)

        if len(lut.table.shape) == 2:
            lut_out = _apply_1d_lut(frame, lut_t)
        else:
            lut_out = _apply_3d_lut(frame, lut_t)

        if log:
            lut_out = lut_out.clamp(min=1e-7).pow(2.2)
        if is_non_default:
            lut_out = ((lut_out - dom_min) / dom_scale).clamp(0.0, 1.0)

        return ((image + strength * (lut_out - image)).clamp(0.0, 1.0),)


NODE_CLASS_MAPPINGS = {
    "ProPostVignetteTorched": ProPostVignette,
    "ProPostFilmGrainTorched": ProPostFilmGrain,
    "ProPostRadialBlurTorched": ProPostRadialBlur,
    "ProPostDepthMapBlurTorched": ProPostDepthMapBlur,
    "ProPostApplyLUTTorched": ProPostApplyLUT,
}

NODE_DISPLAY_NAME_MAPPINGS = {
    "ProPostVignetteTorched": "ProPost Vignette (Torched)",
    "ProPostFilmGrainTorched": "ProPost Film Grain (Torched)",
    "ProPostRadialBlurTorched": "ProPost Radial Blur (Torched)",
    "ProPostDepthMapBlurTorched": "ProPost Depth Map Blur (Torched)",
    "ProPostApplyLUTTorched": "ProPost Apply LUT (Torched)",
}

__all__ = ["NODE_CLASS_MAPPINGS", "NODE_DISPLAY_NAME_MAPPINGS"]
