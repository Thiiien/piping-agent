"""Extra clean stills of the finished 3D model for the pitch deck (no captions)."""
import math

import numpy as np
from PIL import Image

import render_video as rv


def shot(name, elev, azim, dist, wc, focal=rv.F1, size=None):
    t = rv.T_ORBIT                      # everything built, drawing dimmed
    rv.render_scene(t)                  # sets actor states for time t
    e, a = math.radians(elev), math.radians(azim)
    dirv = np.array([math.cos(e) * math.cos(a), math.cos(e) * math.sin(a), math.sin(e)])
    up = np.array([-math.sin(e) * math.cos(a), -math.sin(e) * math.sin(a), math.cos(e)])
    cam = rv.pl.camera
    cam.position, cam.focal_point, cam.up = focal + dist * dirv, focal, up
    cam.SetWindowCenter(*wc)
    rv.pl.renderer.ResetCameraClippingRange()
    rv.pl.render()
    im = Image.fromarray(rv.pl.screenshot(return_img=True))
    if size:
        im = im.crop(size)
    im.save(rv.OUT / name)
    print("saved", name, im.size)


rv.pl.enable_anti_aliasing("ssaa")
rv.pl.enable_ssao(radius=900, bias=25, kernel_size=64, blur=True)
# title slide: model pushed to the right, text goes on the left
shot("hero_title.png", 27, -40, 80000, (-0.47, 0.0))
# before/after slide: model centred, cropped to 4:3-ish
shot("hero_model.png", 28, -42, 50000, (0.0, 0.04), size=(240, 60, 1680, 1080))
