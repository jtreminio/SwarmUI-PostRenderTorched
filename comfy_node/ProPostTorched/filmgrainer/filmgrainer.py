# Filmgrainer - by Lars Ole Pontoppidan - MIT License
# Torch-based rewrite - by Juan Treminio - MIT License

import torch
import torch.nn.functional as F
from . import graingamma
from . import graingen

_GRAIN_TYPES = {
    1: (0.8,    63),   # Fine
    2: (1.0,    45),   # Fine Simple
    3: (1.5,    50),   # Coarse
    4: (1.6666, 50),   # Coarser
}

# PIL SHARPEN kernel reproduced: center=9, surrounding=-1, sum=1
_SHARPEN_KERNEL = torch.tensor(
    [[-1., -1., -1.],
     [-1.,  9., -1.],
     [-1., -1., -1.]]
).view(1, 1, 3, 3)


def _sharpen_pass(image_4d: torch.Tensor) -> torch.Tensor:
    """image_4d: [1, C, H, W] float32. Matches one PIL ImageFilter.SHARPEN pass."""
    C = image_4d.shape[1]
    k = _SHARPEN_KERNEL.to(image_4d.device).expand(C, 1, 3, 3)
    return F.conv2d(image_4d, k, padding=1, groups=C).clamp(0.0, 1.0)


def process(image: torch.Tensor, scale: float, src_gamma: float,
            grain_power: float, shadows: float, highs: float,
            grain_type: int, grain_sat: float,
            gray_scale: bool, sharpen: int, seed: int) -> torch.Tensor:
    """
    image:   [H, W, 3] float32 in [0, 1], on target device.
    Returns: [H, W, 3] float32 in [0, 1], same device.
    """
    device = image.device
    image = image.clamp(0.0, 1.0)
    org_h, org_w = image.shape[:2]

    if scale != 1.0:
        work = F.interpolate(
            image.permute(2, 0, 1).unsqueeze(0),
            size=(max(1, int(org_h / scale)), max(1, int(org_w / scale))),
            mode='bicubic', align_corners=False
        ).clamp(0.0, 1.0)
    else:
        work = image.permute(2, 0, 1).unsqueeze(0)  # [1, 3, H, W]

    _, _, work_h, work_w = work.shape

    lut = graingamma.Map.calculate(src_gamma, grain_power, shadows, highs, device=device)
    lut_t = lut.map  # [256, 256] uint8

    grain_size, grain_gauss = _GRAIN_TYPES[grain_type]
    sat = -1.0 if gray_scale else grain_sat
    mask = graingen.grainGen(work_w, work_h, grain_size, grain_gauss, sat, seed, device=device)
    # mask: [H, W, 3] uint8 in [0, 255]

    img_u8 = (work.squeeze(0).permute(1, 2, 0) * 255).clamp(0, 255).to(torch.uint8)  # [H, W, 3]

    if gray_scale:
        weights = torch.tensor([0.21, 0.72, 0.07], device=device)
        gray = (img_u8.float() * weights).sum(dim=-1).clamp(0, 255).to(torch.uint8)
        gray_out = lut_t[gray.long(), mask[:, :, 0].long()]
        out = gray_out.unsqueeze(-1).expand(-1, -1, 3).float() / 255.0
    else:
        r = lut_t[img_u8[:, :, 0].long(), mask[:, :, 0].long()]
        g = lut_t[img_u8[:, :, 1].long(), mask[:, :, 1].long()]
        b = lut_t[img_u8[:, :, 2].long(), mask[:, :, 2].long()]
        out = torch.stack([r, g, b], dim=-1).float() / 255.0

    if scale != 1.0:
        out = F.interpolate(
            out.permute(2, 0, 1).unsqueeze(0),
            size=(org_h, org_w), mode='bicubic', align_corners=False
        ).clamp(0.0, 1.0).squeeze(0).permute(1, 2, 0)

    if sharpen > 0:
        out_4d = out.permute(2, 0, 1).unsqueeze(0)
        for _ in range(sharpen):
            out_4d = _sharpen_pass(out_4d)
        out = out_4d.squeeze(0).permute(1, 2, 0)

    return out.clamp(0.0, 1.0)
