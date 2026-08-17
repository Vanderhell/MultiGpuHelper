# Changelog

## [1.1.0] - 2026-08-17

- Separated GPU vendor identity from discovery backend identity.
- Added distinct first-available, most-free-memory, round-robin, and specific-device policies.
- Corrected timeout, callback exception, concurrency, and soft VRAM reservation behavior.
- Added NVIDIA parser and WMI vendor-mapping tests that do not require GPU hardware.
- Documented backend data gaps and platform requirements.
