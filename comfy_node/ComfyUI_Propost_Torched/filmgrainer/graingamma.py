# Filmgrainer - by Lars Ole Pontoppidan - MIT License
# Torch-based rewrite - by Juan Treminio - MIT License

import torch

_ShadowEnd = 160
_HighlightStart = 200

# (src_gamma, noise_power, shadow_level, high_level, device_str) → Map
_lut_cache: dict = {}


class Map:
    def __init__(self, lut: torch.Tensor):
        self.map = lut  # [256, 256] uint8

    @staticmethod
    def calculate(src_gamma, noise_power, shadow_level, high_level,
                  device: torch.device = None) -> 'Map':
        if device is None:
            device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')

        cache_key = (src_gamma, noise_power, shadow_level, high_level, str(device))
        if cache_key in _lut_cache:
            return _lut_cache[cache_key]

        sv = torch.arange(256, dtype=torch.float32, device=device)
        nv = torch.arange(256, dtype=torch.float32, device=device)

        # Gamma-compensate source values: pic = (sv/255)^(1/src_gamma) * 255
        pic = (sv / 255.0).clamp(min=1e-7).pow(1.0 / src_gamma) * 255.0

        # Headroom cropping
        crop_top = noise_power * high_level / 12.0
        crop_low = noise_power * shadow_level / 20.0
        pic_scale = 1.0 - (crop_top + crop_low)
        pic_offs = 255.0 * crop_low

        # Per-source-value noise gamma varies linearly with pic_value
        gamma = pic * (1.5 / 256.0) + 0.5
        gamma_offset = (128.0 / 255.0) ** (1.0 / gamma)

        # Development curve: piecewise-linear shadow/mid/highlight
        power = torch.full((256,), 0.5, dtype=torch.float32, device=device)
        shadow_mask = pic < _ShadowEnd
        high_mask = pic >= _HighlightStart
        power[shadow_mask] = (
            0.5 - (_ShadowEnd - pic[shadow_mask]) * (0.5 - shadow_level) / _ShadowEnd
        )
        power[high_mask] = (
            0.5 - (pic[high_mask] - _HighlightStart) * (0.5 - high_level) / (255 - _HighlightStart)
        )

        # gamma_compensated[i, j] = (nv[j]/255)^(1/gamma[i]) - gamma_offset[i]
        # Broadcast outer product via exp(log(nv/255) * (1/gamma))
        log_nv = (nv / 255.0).clamp(min=1e-7).log()
        inv_gamma = (1.0 / gamma).unsqueeze(1)
        gamma_compensated = torch.exp(log_nv.unsqueeze(0) * inv_gamma) - gamma_offset.unsqueeze(1)

        lut = (
            pic.unsqueeze(1) * pic_scale
            + pic_offs
            + 255.0 * power.unsqueeze(1) * noise_power * gamma_compensated
        ).clamp(0, 255).to(torch.uint8)

        result = Map(lut)
        _lut_cache[cache_key] = result
        return result

    def to(self, device):
        self.map = self.map.to(device)
        return self

    def lookup(self, pic_value: int, noise_value: int) -> int:
        return int(self.map[pic_value, noise_value])

    def saveToFile(self, filename):
        from PIL import Image
        img = Image.fromarray(self.map.cpu().numpy())
        img.save(filename)


if __name__ == "__main__":
    import matplotlib.pyplot as plt
    import numpy as np

    def _gammaCurve(gamma, x):
        return pow((x / 255.0), (1.0 / gamma))

    def _calcDevelopment(shadow_level, high_level, x):
        if x < _ShadowEnd:
            return 0.5 - (_ShadowEnd - x) * (0.5 - shadow_level) / _ShadowEnd
        elif x < _HighlightStart:
            return 0.5
        else:
            return 0.5 - (x - _HighlightStart) * (0.5 - high_level) / (255 - _HighlightStart)

    def plotfunc(x_min, x_max, step, func):
        x_all = np.arange(x_min, x_max, step)
        y = [func(x) for x in x_all]
        plt.figure()
        plt.plot(x_all, y)
        plt.grid()

    plotfunc(0.0, 255.0, 1.0, lambda x: _calcDevelopment(0.2, 0.3, x))
    plotfunc(0.0, 255.0, 1.0, lambda x: _gammaCurve(0.5, x))
    plotfunc(0.0, 255.0, 1.0, lambda x: _gammaCurve(1, x))
    plotfunc(0.0, 255.0, 1.0, lambda x: _gammaCurve(2, x))
    plt.show()
