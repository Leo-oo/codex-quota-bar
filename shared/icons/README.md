# Selected option-2 quota mark

The two SVGs reconstruct the user-selected option-2 reference with a continuous round-cap arc, a filled circular hub, and a 45-degree upper-right needle. The SVGs contain two paths and one circle. Geometry and provenance are recorded in `geometry.json` and `provenance.json`; output sizes and SHA-256 hashes are in `asset-manifest.json`.

## Geometry and colors

- View box: 100 × 100; canonical center: (50, 58).
- Arc: radius 41, stroke 8, 260-degree sweep from 140 to 40 degrees; round caps.
- Filled hub: radius 8. Needle: (50, 58) → (70, 38), stroke 8, round caps.
- A vertical translation of about −0.677146 centers the complete visible bounding box on the artboard. Primitive proportions are preserved.
- Black: `#000000`; white: `#FFFFFF`. PNG backgrounds use real alpha transparency.

The private reference crop and previews remain outside the source repository. Reference filename/hash are retained without a personal filesystem path. The reconstructed vector geometry is the source; no original client bitmap or trademark path is included.

## Export layout

`quota-mark-black.svg` and `quota-mark-white.svg` are the shared vector sources.

`png/{black,white}/quota-mark-{size}.png` contains independent exports at 16, 18, 20, 22, 24, 32, 40, 48, 64, 128, 256, 512 and 1024 pixels. PNG antialiasing is generated from the same analytic geometry, with alpha-only Lanczos filtering and exact black/white RGB.

The Windows assets in `../../windows/src/assets/` are:

| File | Variant |
| --- | --- |
| `quota-tray-dark.ico` | White for a dark tray |
| `quota-tray-light.ico` | Black for a light tray |
| `quota-app.ico` | Black program icon |

Each ICO has 10 original frames: 16, 18, 20, 22, 24, 32, 40, 48, 64 and 256 pixels. Every frame is classic 32-bit BGRA DIB with a 40-byte bitmap header, bottom-up color data, and a padded 1-bit AND transparency mask. ICO frames contain no PNG compression.

`macos/quota-mark-{black,white}.iconset/` each contains the 10 standard PNG filenames from 16 through 512@2x (1024 pixels). No ICNS is included, and these assets have not been tested on macOS.

The Python/Pillow renderer and private validation scripts are retained with the local validation evidence. Consumers of the committed SVG/PNG/ICO files do not need Python or Pillow. Project licensing is handled by the repository's notices; this resource specification does not grant a separate license to external reference artwork.

