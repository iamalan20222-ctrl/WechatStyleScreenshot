# Third-Party Notices

## TesseractOCR and trained data

The TesseractOCR .NET wrapper, Tesseract OCR engine, and the `chi_sim` and `eng` trained data are distributed under the Apache License 2.0. The trained data is from the `tesseract-ocr/tessdata_fast` repository. License texts are included in this source tree and the Windows publish directory.

Versions: TesseractOCR NuGet 5.5.2; tessdata_fast commit `87416418657359cb625c412a48b6e1d6d41c29bd`.

Leptonica is distributed under the BSD 2-Clause license. Its license text is included as `src/WechatStyleScreenshot/licenses/Leptonica-BSD-2-Clause.txt` and in the publish directory.

- TesseractOCR: https://github.com/Sicos1977/TesseractOCR
- Tesseract: https://github.com/tesseract-ocr/tesseract
- tessdata_fast: https://github.com/tesseract-ocr/tessdata_fast

## Visual C++ Runtime

The Windows publish output includes unmodified Microsoft Visual C++ Runtime DLLs through the `ThinkGeo.Dependency.MicrosoftVisualCRunTime140` NuGet package (version 14.5.4). Redistribution is governed by the [Microsoft Visual Studio 2022 Redistributable Code license terms](https://visualstudio.microsoft.com/license-terms/vs2022-cruntime/). These files are deployed app-local; the application does not install or modify system components.

## Apple Vision

The macOS build uses Apple's system Vision framework and does not bundle third-party OCR binaries or language data.
