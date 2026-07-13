# Changelog

## [1.1.0](https://github.com/benjamin-bader/stiletto/compare/v1.0.0...v1.1.0) (2026-07-13)


### Features

* **generator:** eagerly register cross-assembly loaders at Create anchors ([#49](https://github.com/benjamin-bader/stiletto/issues/49)) ([d1f76da](https://github.com/benjamin-bader/stiletto/commit/d1f76da9c611e1c998551bab2af4ed85ac7dafeb))

## [1.0.0](https://github.com/benjamin-bader/stiletto/compare/v1.0.0-alpha.1...v1.0.0) (2026-07-12)


### ⚠ BREAKING CHANGES

* The Fody-based weaver is removed. Consumers must drop the Stiletto.Fody package and its FodyWeavers.xml entry; the source generator ships in the Stiletto package and runs automatically. 1.0 targets net10.0 only. The public API ([Inject]/[Module]/[Provides]/[Named]/[Singleton]/Container.Create) is unchanged.

### Features

* replace the Fody weaver with a Roslyn source generator ([#45](https://github.com/benjamin-bader/stiletto/issues/45)) ([d0d99a6](https://github.com/benjamin-bader/stiletto/commit/d0d99a612afe9edbeeaf89646cb8f7be0e8af8b2))


### Bug Fixes

* don't misclassify keys that merely nest Provider/Lazy/Set ([#42](https://github.com/benjamin-bader/stiletto/issues/42)) ([4fd3f92](https://github.com/benjamin-bader/stiletto/commit/4fd3f923eedc5e4aa422af599ce36043ef78ded8))


### Documentation

* graduate to 1.0.0 and clarify prerelease graduation ([#44](https://github.com/benjamin-bader/stiletto/issues/44)) ([65db585](https://github.com/benjamin-bader/stiletto/commit/65db585942a3ed13402d0a34d3b37698c6450b03))
