# Image Upload Review Fixes Design

## Goal

Address the two actionable findings in `Reviews/2026-10-16-repository-review.md`: bounded image-processing memory use and CI failure on detected vulnerable NuGet packages.

## Design

Keep the existing 2 MB file limit and 8000 px per-dimension limit. Add a 16 MP total-pixel limit based on dimensions read from the image header, before decoding. Reject over-limit images through the existing `InvalidImageException` path.

Generate thumbnails with the original aspect ratio while bounding both dimensions to 800 px wide and 100 px high. This preserves the existing 100 px target height for ordinary images while preventing extreme panoramas from producing unbounded output. Add tests for the limits, typical aspect ratios, and extreme aspect ratios. Update the English and Japanese README to describe the actual constraints.

Keep the existing `dotnet list package --vulnerable --include-transitive` CI check and make the step fail when its output reports a vulnerability severity. Do not add a new package or external scanning service.

## Verification

- Run focused image processor tests, then the full test suite and Release build.
- Exercise the CI severity-detection condition against both a clean report and a report containing a vulnerability.
- Check the final diff and ensure the two README files remain consistent.

## Out of scope

Changing authentication, storage architecture, image formats, or the existing per-dimension and upload-size limits.
