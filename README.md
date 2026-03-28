# SwarmUI-PostRenderTorched

A [SwarmUI](https://github.com/mcmonkeyprojects/SwarmUI/) extension that adds parameters for [ProPost Torched](https://github.com/digitaljohn/comfyui-propost/).

This version is a fork of [HellerCommaA/SwarmUI-PostRender](https://github.com/HellerCommaA/SwarmUI-PostRender), refactored to be Torch-driven instead of CPU-driven. Benchmarks show a significant reduction in generation time penalty - while not literally free, using this extension is the closest to "free" as anything in computers can get.

# Benefits over original [HellerCommaA/SwarmUI-PostRender](https://github.com/HellerCommaA/SwarmUI-PostRender)

In practice, this extension gives you a much more SwarmUI-friendly version of ProPost:

* The post effects are torch-driven, so they run far faster than the original CPU-oriented version and feel much more practical to leave enabled in real workflows
* Film Grain, Vignette, Depth Map Blur, and Radial Blur all include built-in presets, so you can start from good looks instead of dialing every slider from scratch
* The ComfyUI node dependency is bundled with the extension, no extra Python dependencies needed other than what ComfyUI already uses
* A set of LUTs is also baked in, so the LUT effect has useful options available out of the box instead of starting empty

# Benchmarks

For a batch of 9 images:

* z-image-base + z-image-turbo for final resolution of 2592x3792
* Prompt "Young woman, smiling, looking at viewer, in a scenic location."
* starting seed of `42`
* AMD Ryzen 9 9950X3D 16-Core CPU
* 128gb DDR5 RAM
* RTX Pro 6000 96gb VRAM

the generated speeds are:

Base, without any post-rendering:
* Generated an image in 0.00 sec (prep) and 19.92 sec (gen)
* Generated an image in 19.90 sec (prep) and 19.92 sec (gen)
* Generated an image in 39.79 sec (prep) and 20.17 sec (gen)
* Generated an image in 59.94 sec (prep) and 19.78 sec (gen)
* Generated an image in 80.10 sec (prep) and 20.57 sec (gen)
* Generated an image in 100.65 sec (prep) and 20.74 sec (gen)
* Generated an image in 2.02 min (prep) and 20.68 sec (gen)
* Generated an image in 2.37 min (prep) and 20.87 sec (gen)
* Generated an image in 2.71 min (prep) and 20.76 sec (gen)

**183.4 total generation time**

Then compare with both PostRender and PostRenderTorched using:
* Film Grain
    * Type: Fine Simple
    * Saturation: 0.3
    * Power: 0.3
    * Shadows: 0.35
    * Highlights: 0.1
    * Scale: 1
    * Sharpen: 0
    * Source Gamma: 1
* Vignette
    * Strength: 0.42
    * X Position: 0.5
    * Y Position: 0.5
* LUT
    * Film Emulation/Kodak Professional Portra 400.cube
    * Strength: 0.6
    * LOG Space: false

[HellerCommaA/SwarmUI-PostRender](https://github.com/HellerCommaA/SwarmUI-PostRender):
* Generated an image in 0.00 sec (prep) and 24.77 sec (gen)
* Generated an image in 24.75 sec (prep) and 25.14 sec (gen)
* Generated an image in 49.87 sec (prep) and 25.54 sec (gen)
* Generated an image in 75.39 sec (prep) and 25.47 sec (gen)
* Generated an image in 100.84 sec (prep) and 25.31 sec (gen)
* Generated an image in 2.10 min (prep) and 25.46 sec (gen)
* Generated an image in 2.53 min (prep) and 25.49 sec (gen)
* Generated an image in 2.95 min (prep) and 25.42 sec (gen)
* Generated an image in 3.37 min (prep) and 25.48 sec (gen)

**228.0 total generation time, 24.3% increase**

[jtreminio/SwarmUI-PostRenderTorched](https://github.com/jtreminio/SwarmUI-PostRenderTorched) (this repo):
* Generated an image in 0.01 sec (prep) and 21.06 sec (gen)
* Generated an image in 21.04 sec (prep) and 21.33 sec (gen)
* Generated an image in 42.27 sec (prep) and 21.58 sec (gen)
* Generated an image in 63.83 sec (prep) and 21.55 sec (gen)
* Generated an image in 85.35 sec (prep) and 21.69 sec (gen)
* Generated an image in 107.16 sec (prep) and 22.08 sec (gen)
* Generated an image in 2.15 min (prep) and 21.93 sec (gen)
* Generated an image in 2.52 min (prep) and 21.79 sec (gen)
* Generated an image in 2.88 min (prep) and 21.94 sec (gen)

**195.0 total generation time, 6.3% increase**

As shown, [jtreminio/SwarmUI-PostRenderTorched](https://github.com/jtreminio/SwarmUI-PostRenderTorched) is approximately **74% faster** in the post-processing stage when compared to [HellerCommaA/SwarmUI-PostRender](https://github.com/HellerCommaA/SwarmUI-PostRender).
