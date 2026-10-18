# Lox Lorax public-release plan

This repository is prepared for patch release **1.0.8**. Pushing `main` and `v1.0.8` will trigger `.github/workflows/release.yml`, validate the committed packages, and create the GitHub release with both ZIPs and their checksums.

## 1. Publisher and rights gate

- [x] Use `ErFre` as the public GitHub publisher, matching the repository owner.
- [ ] Review [DISCLAIMER.md](DISCLAIMER.md) and decide whether to distribute a recognizable fan interpretation of The Lorax. The mesh is original, but the character likeness is derivative; a disclaimer is not permission from the rights holder.
- [x] Keep the release free and clearly unofficial in the README, package copy, and disclaimer.
- [x] Include the required generative-AI disclosure. For Nexus Mods, select **AI-Generated Content** because code and in-game assets received substantial generative-AI assistance.
- [ ] Review the art-asset terms in [LICENSE.md](LICENSE.md), replacing them before publication if you prefer broader reuse.

## 2. Create the GitHub repository

- [x] Create the GitHub repository `Erfre/LoxLorax`.
- [x] Place the release contents at the repository root.
- [ ] Set the repository description to: `Animated Lorax-style lox and undead Vile visual replacement for Valheim.`
- [ ] Add topics: `valheim`, `valheim-mod`, `bepinex`, `lox`, `visual-replacer`.
- [x] Commit the prepared source, documentation, screenshots, icon, metadata, binaries, and release archives.
- [ ] Push the release commit to GitHub.
- [x] Set `website_url` in `manifest.json` to `https://github.com/Erfre/LoxLorax`.
- [x] Rebuild both upload ZIPs so they contain the repository URL and final package documentation.

## 3. Final verification

- [x] Close Valheim before rebuilding.
- [x] Build the DLL without warnings against Valheim 1.0.20 and BepInExPack 5.4.2351.
- [x] Validate the living model with `python3 tools/validate_model.py`.
- [x] Validate the Vile model with `python3 tools/validate_model.py assets/vile`.
- [x] Complete the isolated living-lox world regression, including live F8 disable and re-enable.
- [x] Complete the isolated Vile regression across 30 clips, 150 sampled poses, and 60 locomotion poses.
- [x] Rebuild the public packages with `python3 tools/build_public_packages.py`.
- [x] Validate the repository and archives with `python3 tools/validate_release.py`.
- [x] Confirm `release-artifacts/SHA256SUMS.txt` matches both ZIP files.
- [ ] Install the manual ZIP into a clean mod-manager profile and perform one final launch.

## 4. Publish GitHub release first

- [x] Create local tag `v1.0.8` from the tested release commit.
- [ ] Push `main` and tag `v1.0.8`.
- [x] Configure the tag workflow to create a GitHub Release titled `Lox Lorax 1.0.8` using `publishing/RELEASE_NOTES.md`.
- [x] Configure the tag workflow to attach both ZIPs and `SHA256SUMS.txt` from `release-artifacts/`.
- [ ] Confirm the README images render and the release assets download without authentication.

GitHub Releases provide the canonical source and manual-download location. GitHub documents releases as the place to publish versioned software and attach binary files: <https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases>.

## 5. Publish mod-manager packages

### Thunderstore / r2modman

- [x] Create or select a Thunderstore team.
- [ ] Upload `release-artifacts/LoxLorax-1.0.8-thunderstore-hexium.zip` to the Valheim community.
- [x] Select client-side, visual/cosmetic, enemy, and AI-generated categories.
- [x] Confirm the BepInEx dependency appears.
- [ ] Install it through r2modman into a clean profile and launch once.

Thunderstore requires `manifest.json`, `README.md`, and an exact 256×256 `icon.png` at the ZIP root. See <https://wiki.thunderstore.io/mods/creating-a-package>.

### Hexium / Gale

- [ ] Create or select a Hexium team.
- [ ] Upload the same Thunderstore-compatible ZIP.
- [ ] Mark it client-side.
- [ ] Confirm the listing in Gale and install it into a clean profile.

Hexium accepts Thunderstore-compatible packages and assumes BepInExPack for Valheim: <https://hexium.gg/packaging>.

## 6. Publish Nexus Mods

- [ ] Create the Valheim mod page using `publishing/NEXUS_DESCRIPTION.md`.
- [ ] Apply accurate visual-replacer and AI-generated-content tags.
- [ ] Upload `release-artifacts/LoxLorax-1.0.8-manual.zip` as the main file.
- [ ] Upload `media/cover.png` as the thumbnail and the images from `media/screenshots/` as the gallery.
- [ ] State that BepInExPack Valheim is required and this is a client-side mod.
- [ ] Link the GitHub repository and report the tested game/BepInEx versions.
- [ ] Recheck the page while logged out.

Nexus requires accurate categorization and generative-AI disclosure, and holds uploaders responsible for rights to copyrighted content. Review its current file rules before submitting: <https://help.nexusmods.com/article/28-file-submission-guidelines>.

## 7. Announce and support

- [ ] Use `publishing/ANNOUNCEMENT.md` for a concise post after all download links exist.
- [ ] Post only where self-promotion rules permit, such as the relevant Valheim modding Discord channel or subreddit thread.
- [ ] Enable GitHub Issues and use the included bug-report form.
- [ ] Ask reports to include mod version, Valheim version, BepInExPack version, other mods, reproduction steps, and a redacted log excerpt.
- [ ] Never ask users for entire save directories or unredacted logs.

## 8. Updating after release

- [ ] Increment the BepInEx plugin version and `manifest.json` together.
- [ ] Add a new changelog entry.
- [ ] Rebuild, repeat both world tests, package, and validate.
- [ ] Publish the GitHub release, then upload the same version to Thunderstore, Hexium, and Nexus.
- [ ] Never replace an already-published archive silently; publish a new semantic version.
