jsQR 1.4.0 (Apache-2.0), `dist/jsQR.js` from the `jsqr` package on npm (`npm pack jsqr@1.4.0`), with the package's LICENSE. Source: https://github.com/cozmo/jsQR. It reads a QR code from picture data, and `js/scan.js` loads it only on a browser that has no `BarcodeDetector`, so other pages and browsers don't download it.

SHA-384 of `jsQR.js`, as it came in the package:

    6f961ae01ab7a82cb3dfd9b621287ee03c6300896375e1702bf06c5cbba3f60ba068dc00723fe26b5e5fc4d64bf4d971

To update, run `npm pack jsqr@<version>`, replace both files and change the version and hash here.
