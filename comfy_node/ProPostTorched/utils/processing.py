import torch
import torch.nn.functional as F

# Kernel cache: (kernel_size, device_str, dtype_str) -> [k] tensor
_kernel_cache: dict = {}


def _gaussian_kernel_1d(kernel_size: int, device: torch.device,
                        dtype: torch.dtype) -> torch.Tensor:
    """[k] Gaussian weights matching cv2.GaussianBlur(img, (k, k), 0) sigma."""
    key = (kernel_size, str(device), str(dtype))
    if key not in _kernel_cache:
        sigma = 0.3 * ((kernel_size - 1) * 0.5 - 1) + 0.8  # OpenCV's sigma formula for sigmaX=0
        x = torch.arange(kernel_size, dtype=torch.float32, device=device) - kernel_size // 2
        g = torch.exp(-0.5 * (x / sigma) ** 2)
        g /= g.sum()
        _kernel_cache[key] = g.to(dtype=dtype)
    return _kernel_cache[key]


def gaussian_blur(image_4d: torch.Tensor, kernel_size: int) -> torch.Tensor:
    """
    image_4d: [N, C, H, W] float32.
    Depthwise separable Gaussian blur - one kernel applied independently per channel.
    """
    if kernel_size <= 1:
        return image_4d

    kernel_1d = _gaussian_kernel_1d(kernel_size, image_4d.device, image_4d.dtype)
    C = image_4d.shape[1]
    kernel_x = kernel_1d.view(1, 1, 1, kernel_size).expand(C, 1, 1, kernel_size)
    kernel_y = kernel_1d.view(1, 1, kernel_size, 1).expand(C, 1, kernel_size, 1)

    blurred = F.conv2d(image_4d, kernel_x, padding=(0, kernel_size // 2), groups=C)
    return F.conv2d(blurred, kernel_y, padding=(kernel_size // 2, 0), groups=C)


def generate_blurred_images(image_4d: torch.Tensor, blur_strength: float,
                            steps: int, focus_spread: float = 1.0) -> list:
    """image_4d: [B, C, H, W]. Returns list of `steps` blurred tensors."""
    blurred = []
    blurred_by_kernel = {}
    for step in range(1, steps + 1):
        blur_factor = (step / steps) ** focus_spread * blur_strength
        k = max(1, int(blur_factor))
        k = k if k % 2 == 1 else k + 1
        if k not in blurred_by_kernel:
            blurred_by_kernel[k] = gaussian_blur(image_4d, k)
        blurred.append(blurred_by_kernel[k])
    return blurred


def apply_blurred_images(image_4d: torch.Tensor, blurred_images: list,
                         mask_2d: torch.Tensor) -> torch.Tensor:
    """
    image_4d:       [B, C, H, W]
    blurred_images: list of [B, C, H, W]
    mask_2d:        [H, W], [B, H, W], or [B, 1, H, W] in [0, 1]
    Returns:        [B, C, H, W]
    """
    steps = len(blurred_images)
    step_size = 1.0 / steps

    if mask_2d.dim() == 2:
        mask = mask_2d.unsqueeze(0).unsqueeze(0)
    elif mask_2d.dim() == 3:
        mask = mask_2d.unsqueeze(1)
    else:
        mask = mask_2d

    final = torch.zeros_like(image_4d)

    for i, blurred in enumerate(blurred_images):
        current = ((mask - i * step_size) * steps).clamp(0.0, 1.0)
        nxt = ((mask - (i + 1) * step_size) * steps).clamp(0.0, 1.0)
        final += (current - nxt) * blurred

    final += (1.0 - (mask * steps).clamp(0.0, 1.0)) * image_4d
    return final
