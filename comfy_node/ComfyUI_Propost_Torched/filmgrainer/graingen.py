# Filmgrainer - by Lars Ole Pontoppidan - MIT License
# Torch-based rewrite - by Juan Treminio - MIT License

import torch
import torch.nn.functional as F

# In-memory grain cache: (W, H, sat_key, grain_size, power, seed, device_str) → [H, W, 3] uint8
_cache: dict = {}


def grainGen(width: int, height: int, grain_size: float, power: float,
             saturation: float, seed: int = 1,
             device: torch.device = None) -> torch.Tensor:
    """
    Returns [H, W, 3] uint8 tensor on `device` with grain noise values in [0, 255].
    saturation < 0 produces grayscale (identical R/G/B channels).
    """
    if device is None:
        device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')

    sat_key = -1.0 if saturation < 0.0 else saturation
    cache_key = (width, height, sat_key, grain_size, power, seed, str(device))
    if cache_key in _cache:
        return _cache[cache_key]

    noise_w = max(1, int(width / grain_size))
    noise_h = max(1, int(height / grain_size))

    generator_device = device if device.type in {"cpu", "cuda"} else torch.device("cpu")
    gen = torch.Generator(device=generator_device)
    gen.manual_seed(seed)

    def _randn(size, mean: float, std: float) -> torch.Tensor:
        noise = torch.randn(size, generator=gen, device=generator_device, dtype=torch.float32)
        noise = noise * std + mean
        if generator_device != device:
            noise = noise.to(device)
        return noise

    if saturation < 0.0:
        noise = _randn((noise_h, noise_w, 1), mean=128.0, std=power)
        noise = noise.clamp(0, 255).expand(-1, -1, 3).contiguous()
    else:
        intens_power = power * (1.0 - saturation)
        intens = _randn((noise_h, noise_w, 1), mean=128.0, std=intens_power)
        chroma = _randn((noise_h, noise_w, 3), mean=0.0, std=power)
        noise = (chroma * saturation + intens).clamp(0, 255)

    if noise_w != width or noise_h != height:
        # [H, W, C] → [1, C, H, W] for interpolate, back to [H, W, C]
        noise = F.interpolate(
            noise.permute(2, 0, 1).unsqueeze(0).float(),
            size=(height, width), mode='bilinear', align_corners=False
        ).squeeze(0).permute(1, 2, 0)

    result = noise.clamp(0, 255).to(device=device, dtype=torch.uint8)
    _cache[cache_key] = result
    return result


if __name__ == "__main__":
    import sys
    from PIL import Image
    import numpy as np
    if len(sys.argv) == 8:
        w = int(sys.argv[2])
        h = int(sys.argv[3])
        gs = float(sys.argv[4])
        pw = float(sys.argv[5])
        sat = float(sys.argv[6])
        sd = int(sys.argv[7])
        out = grainGen(w, h, gs, pw, sat, sd, device=torch.device('cpu'))
        img = Image.fromarray(out.numpy())
        img.save(sys.argv[1])
