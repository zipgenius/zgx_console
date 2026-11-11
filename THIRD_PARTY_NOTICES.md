# Third-party components

The GPL-3.0-only license applies to the original ZipGenius Console and SFX code.
Third-party components retain their own licenses.

* 7-Zip native binaries: see `native/sfx_stub/third_party/License.txt` (LGPL, BSD and unRAR restrictions). Upstream source: https://www.7-zip.org/ and https://www.7-zip.org/download.html. Keep these notices in redistributed packages and meet the corresponding-source obligations for the exact binary versions you distribute.
* The unused AGPL Json.NET Schema dependency was removed during extraction.
* NuGet dependencies and their transitive dependencies are restored by .NET. Their license expressions, license files and upstream repository links are recorded in the restored `.nuspec` files. `build/package.py` copies these into every distribution.
* SQX native binaries and the SQX bridge are intentionally excluded: the source repository did not include their redistribution terms. SQX requires separately supplied compatible native libraries on Windows; it is unavailable in the public packages.

The checked-in 7-Zip binaries and legacy native SFX stub originate from the parent project. The stub source is included under `native/sfx_stub`; Windows CI rebuilds the stub before packaging. Before public release, verify the provenance and exact corresponding source of the inherited 7-Zip binaries.
