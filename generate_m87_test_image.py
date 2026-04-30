"""
generate_m87_test_image.py
Generates a realistic synthetic astrophotography FITS + PNG of M87 / Virgo Cluster.
Output: C:\\Users\\simon\\source\\repos\\astrofinder\\AstroFinder.App\\Resources\\Test\\

Dependencies:
    pip install astropy astroquery numpy matplotlib Pillow scipy
"""

import warnings
import numpy as np
from pathlib import Path
from scipy.ndimage import gaussian_filter

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.colors import PowerNorm

from astropy.wcs import WCS
from astropy.io import fits
from astropy import units as u
from astropy.coordinates import SkyCoord
from astroquery.vizier import Vizier

warnings.filterwarnings("ignore")

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------
OUTPUT_DIR = Path(r"C:\Users\simon\source\repos\astrofinder\AstroFinder.App\Resources\Test")
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

FITS_PATH = OUTPUT_DIR / "m87_synthetic.fits"
PNG_PATH  = OUTPUT_DIR / "m87_synthetic.png"

# Image dimensions (pixels)
IMG_W, IMG_H = 2048, 2048

# Field of view: ~2 degrees wide
FOV_DEG = 2.0
PIXEL_SCALE = FOV_DEG / IMG_W  # degrees per pixel

# M87 centre (J2000)
RA_CENTER  = 187.70592   # 12h 30m 49s
DEC_CENTER = 12.39111    # +12deg 23' 28"

# PSF FWHM for stars (pixels) -- simulate ~2" seeing at ~3.5"/px
PSF_FWHM_PX = 2.2
PSF_SIGMA   = PSF_FWHM_PX / 2.355

# Magnitude limits
MAG_STAR_LIMIT   = 14.5   # Tycho-2 query limit
MAG_GALAXY_LIMIT = 14.0

# Flux ∝ 10^(-(mag) / 2.5); ZP_ADU = peak ADU for a mag-0 star
ZP_ADU       = 1_200_000.0
READ_NOISE   = 12.0       # electrons rms
DARK_CURRENT = 2.0        # ADU per pixel
SKY_BACKGROUND = 280.0    # ADU sky glow

# ---------------------------------------------------------------------------
# Build WCS (north up, east left — standard astronomical orientation)
# ---------------------------------------------------------------------------
def build_wcs() -> WCS:
    w = WCS(naxis=2)
    w.wcs.crpix = [IMG_W / 2.0 + 0.5, IMG_H / 2.0 + 0.5]
    w.wcs.cdelt = [-PIXEL_SCALE, PIXEL_SCALE]   # negative RA -> east left
    w.wcs.crval = [RA_CENTER, DEC_CENTER]
    w.wcs.ctype = ["RA---TAN", "DEC--TAN"]
    w.wcs.equinox = 2000.0
    return w


# ---------------------------------------------------------------------------
# Coordinate -> pixel helpers
# ---------------------------------------------------------------------------
def sky_to_pix(ra_deg, dec_deg, wcs: WCS):
    px, py = wcs.all_world2pix(ra_deg, dec_deg, 0)
    return float(px), float(py)


def in_bounds(x, y, margin=0):
    return (margin <= x < IMG_W - margin) and (margin <= y < IMG_H - margin)


# ---------------------------------------------------------------------------
# Render a circular Gaussian PSF stamp onto the image array (in-place)
# ---------------------------------------------------------------------------
def add_gaussian_source(image, cx, cy, peak_adu, sigma_px):
    r = int(max(6 * sigma_px, 5))
    x0 = int(round(cx)) - r
    x1 = int(round(cx)) + r + 1
    y0 = int(round(cy)) - r
    y1 = int(round(cy)) + r + 1

    ix0, ix1 = max(x0, 0), min(x1, IMG_W)
    iy0, iy1 = max(y0, 0), min(y1, IMG_H)
    if ix0 >= ix1 or iy0 >= iy1:
        return

    yy, xx = np.ogrid[iy0:iy1, ix0:ix1]
    gauss = peak_adu * np.exp(
        -((xx - cx) ** 2 + (yy - cy) ** 2) / (2 * sigma_px ** 2)
    )
    image[iy0:iy1, ix0:ix1] += gauss


# ---------------------------------------------------------------------------
# Render an elliptical galaxy blob (de Vaucouleurs approximation)
# ---------------------------------------------------------------------------
def add_galaxy(image, cx, cy, peak_adu, a_px, b_px, pa_deg=0.0):
    pa_rad = np.radians(pa_deg)
    cos_pa, sin_pa = np.cos(pa_rad), np.sin(pa_rad)

    r = int(max(6 * a_px, 10))
    x0, x1 = max(int(cx) - r, 0), min(int(cx) + r + 1, IMG_W)
    y0, y1 = max(int(cy) - r, 0), min(int(cy) + r + 1, IMG_H)
    if x0 >= x1 or y0 >= y1:
        return

    yy, xx = np.ogrid[y0:y1, x0:x1]
    dx = xx - cx
    dy = yy - cy

    u_rot =  dx * cos_pa + dy * sin_pa
    v_rot = -dx * sin_pa + dy * cos_pa

    r2 = (u_rot / a_px) ** 2 + (v_rot / b_px) ** 2

    # de Vaucouleurs approximation: sum of 4 Gaussians
    blob = (
          0.50 * np.exp(-r2 / (2 * 0.30 ** 2))
        + 0.30 * np.exp(-r2 / (2 * 0.70 ** 2))
        + 0.15 * np.exp(-r2 / (2 * 1.40 ** 2))
        + 0.05 * np.exp(-r2 / (2 * 3.00 ** 2))
    )
    image[y0:y1, x0:x1] += peak_adu * blob


# ---------------------------------------------------------------------------
# Magnitude -> peak ADU
# ---------------------------------------------------------------------------
def mag_to_adu(mag: float) -> float:
    return ZP_ADU * 10 ** (-(mag) / 2.5)


# ---------------------------------------------------------------------------
# Query Tycho-2 stars via Vizier
# ---------------------------------------------------------------------------
def fetch_tycho2_stars(ra, dec, radius_deg, mag_limit):
    print(f"  Querying Tycho-2 (r={radius_deg:.2f} deg, mag<{mag_limit})...")
    Vizier.ROW_LIMIT = 20000
    v = Vizier(columns=["RAmdeg", "DEmdeg", "VTmag"], row_limit=20000)
    try:
        result = v.query_region(
            SkyCoord(ra=ra * u.deg, dec=dec * u.deg, frame="icrs"),
            radius=radius_deg * u.deg,
            catalog="I/259/tyc2",
            column_filters={"VTmag": f"<{mag_limit}"},
        )
    except Exception as e:
        print(f"  Vizier query failed: {e}")
        return np.array([]), np.array([]), np.array([])

    if not result or len(result) == 0:
        print("  No Tycho-2 results -- using fallback synthetic stars.")
        return np.array([]), np.array([]), np.array([])
    tbl = result[0]
    ra_arr  = np.array(tbl["RAmdeg"],  dtype=float)
    dec_arr = np.array(tbl["DEmdeg"],  dtype=float)
    mag_arr = np.array(tbl["VTmag"],   dtype=float)
    ok = np.isfinite(mag_arr)
    print(f"  Got {ok.sum()} stars.")
    return ra_arr[ok], dec_arr[ok], mag_arr[ok]


# ---------------------------------------------------------------------------
# NGC Virgo Cluster galaxies (NED/NGC catalog, within ~3 deg of M87)
# ---------------------------------------------------------------------------
VIRGO_GALAXIES = [
    # name,      RA_deg,     Dec_deg,  Vmag,  a_arcmin, b_arcmin, pa_deg
    ("M87",      187.70592,  12.39111,  8.6,   3.0,      2.5,     155.0),
    ("M84",      186.26584,  12.88694,  9.1,   2.5,      2.2,     140.0),
    ("M86",      186.55042,  12.94611,  8.9,   2.8,      2.4,     130.0),
    ("NGC4438",  186.93625,  13.00500, 10.1,   2.0,      1.0,      20.0),
    ("NGC4387",  186.43208,  12.81000, 12.1,   0.8,      0.5,      90.0),
    ("NGC4402",  186.57333,  13.11278, 11.7,   1.5,      0.6,      90.0),
    ("NGC4425",  186.86708,  12.73472, 11.9,   1.2,      0.6,      30.0),
    ("NGC4461",  187.26625,  13.18500, 11.2,   1.5,      0.8,      10.0),
    ("NGC4473",  187.45417,  13.42861, 10.2,   2.0,      1.4,      95.0),
    ("NGC4476",  187.50000,  12.34972, 12.2,   0.9,      0.7,      25.0),
    ("NGC4478",  187.54875,  12.32722, 11.4,   1.2,      1.0,      20.0),
    ("NGC4486A", 187.72042,  12.29583, 12.5,   0.5,      0.4,     170.0),
    ("NGC4486B", 187.69000,  12.48750, 13.0,   0.4,      0.3,       0.0),
    ("M89",      188.91667,  12.55583, 10.7,   1.6,      1.5,     160.0),
    ("NGC4564",  188.01792,  11.43750, 11.1,   1.5,      0.8,      50.0),
    ("NGC4550",  188.76167,  12.22167, 11.7,   1.5,      0.5,      70.0),
    ("NGC4528",  188.37708,  11.32167, 12.3,   0.9,      0.7,      10.0),
]


# ---------------------------------------------------------------------------
# Main generation routine
# ---------------------------------------------------------------------------
def generate():
    rng = np.random.default_rng(42)

    print("Building WCS...")
    wcs = build_wcs()

    print("Creating blank image...")
    image = np.zeros((IMG_H, IMG_W), dtype=np.float64)

    # Sky background + read noise + dark current
    image += SKY_BACKGROUND
    image += rng.normal(0, READ_NOISE, image.shape)
    image += rng.poisson(DARK_CURRENT, image.shape).astype(float)

    # ------------------------------------------------------------------
    # Render galaxies first (underneath stars)
    # ------------------------------------------------------------------
    print("Rendering Virgo Cluster galaxies...")
    seen = set()
    for name, gra, gdec, vmag, a_am, b_am, pa in VIRGO_GALAXIES:
        key = (round(gra, 4), round(gdec, 4))
        if key in seen:
            continue
        seen.add(key)

        cx, cy = sky_to_pix(gra, gdec, wcs)
        if not in_bounds(cx, cy, margin=-30):
            continue

        a_px = (a_am / 60.0) / PIXEL_SCALE / 2.0
        b_px = (b_am / 60.0) / PIXEL_SCALE / 2.0

        peak = mag_to_adu(vmag) * 0.018
        peak = max(peak, SKY_BACKGROUND * 0.4)

        add_galaxy(image, cx, cy, peak, max(a_px, 1.5), max(b_px, 1.0), pa)
        print(f"  {name:12s}  pix=({cx:.0f},{cy:.0f})  a={a_px:.1f}px  peak={peak:.0f}")

    # ------------------------------------------------------------------
    # Fetch Tycho-2 stars
    # ------------------------------------------------------------------
    query_radius = FOV_DEG * 0.75
    ra_stars, dec_stars, mag_stars = fetch_tycho2_stars(
        RA_CENTER, DEC_CENTER, query_radius, MAG_STAR_LIMIT
    )

    if len(ra_stars) == 0:
        print("  Generating fallback synthetic star field...")
        n_stars = 800
        ra_stars  = rng.uniform(RA_CENTER  - FOV_DEG * 0.5, RA_CENTER  + FOV_DEG * 0.5, n_stars)
        dec_stars = rng.uniform(DEC_CENTER - FOV_DEG * 0.5, DEC_CENTER + FOV_DEG * 0.5, n_stars)
        u_vals    = rng.uniform(0, 1, n_stars)
        mag_stars = 6.0 + (MAG_STAR_LIMIT - 6.0) * u_vals ** (1 / 0.6)

    print(f"Rendering {len(ra_stars)} stars...")
    for ra_s, dec_s, mag_s in zip(ra_stars, dec_stars, mag_stars):
        cx, cy = sky_to_pix(ra_s, dec_s, wcs)
        if not in_bounds(cx, cy, margin=0):
            continue
        peak = mag_to_adu(mag_s)
        sigma = PSF_SIGMA + max(0.0, (8.0 - mag_s) * 0.15)
        add_gaussian_source(image, cx, cy, peak, sigma)

    # ------------------------------------------------------------------
    # Shot noise (Poisson approximation)
    # ------------------------------------------------------------------
    print("Adding shot noise...")
    image = np.clip(image, 0, None)
    noise_scale = np.sqrt(np.maximum(image, 1.0))
    image += rng.normal(0, 1, image.shape) * noise_scale * 0.5

    # ------------------------------------------------------------------
    # Vignetting
    # ------------------------------------------------------------------
    print("Applying vignetting...")
    yy, xx = np.indices((IMG_H, IMG_W))
    cx_c, cy_c = IMG_W / 2.0, IMG_H / 2.0
    r_norm = np.sqrt(((xx - cx_c) / cx_c) ** 2 + ((yy - cy_c) / cy_c) ** 2)
    vignette = 1.0 - 0.08 * r_norm ** 2
    image *= vignette

    # ------------------------------------------------------------------
    # Mild atmospheric blur
    # ------------------------------------------------------------------
    print("Applying atmospheric blur...")
    image = gaussian_filter(image, sigma=0.4)

    image = np.clip(image, 0, 65535).astype(np.float32)

    # ------------------------------------------------------------------
    # Save FITS with full WCS header
    # ------------------------------------------------------------------
    print(f"Saving FITS -> {FITS_PATH}")
    header = wcs.to_header()
    header["BITPIX"]   = -32
    header["NAXIS"]    = 2
    header["NAXIS1"]   = IMG_W
    header["NAXIS2"]   = IMG_H
    header["OBJECT"]   = "M87"
    header["TELESCOP"] = "Synthetic"
    header["INSTRUME"] = "AstroFinder-TestGen"
    header["DATE-OBS"] = "2026-04-27T00:00:00"
    header["EXPTIME"]  = 3600.0
    header["FILTER"]   = "V"
    header["BUNIT"]    = "ADU"
    header["EQUINOX"]  = 2000.0
    header["EPOCH"]    = 2000.0
    header["COMMENT"]  = "Synthetic M87/Virgo test image for AstroFinder plate-solve tests"

    hdu = fits.PrimaryHDU(data=image, header=header)
    hdu.writeto(str(FITS_PATH), overwrite=True)
    print("  FITS saved.")

    # ------------------------------------------------------------------
    # Save PNG (power-law stretch, grayscale)
    # ------------------------------------------------------------------
    print(f"Saving PNG -> {PNG_PATH}")
    fig, ax = plt.subplots(figsize=(10, 10), dpi=200)
    ax.imshow(
        image,
        origin="lower",
        cmap="gray",
        norm=PowerNorm(
            gamma=0.4,
            vmin=np.percentile(image, 1),
            vmax=np.percentile(image, 99.8),
        ),
        interpolation="nearest",
    )
    ax.axis("off")
    fig.subplots_adjust(left=0, right=1, top=1, bottom=0)
    fig.savefig(str(PNG_PATH), dpi=200, bbox_inches="tight", pad_inches=0,
                facecolor="black")
    plt.close(fig)
    print("  PNG saved.")

    print("\nDone.")
    print(f"  FITS : {FITS_PATH}")
    print(f"  PNG  : {PNG_PATH}")
    print(f"  WCS  : RA={RA_CENTER}, Dec={DEC_CENTER}, "
          f"scale={PIXEL_SCALE*3600:.2f}\"/px, FOV={FOV_DEG} deg")


if __name__ == "__main__":
    generate()
