# Changelog

## 1.1.0

- Updated libsm64 to a forked version with extra features and bugfixes
- Implemented teleporters
- Implemented poles
- Added collider clamping (Fixes exteremly large colliders such as default gridspace counting as OOB)
- Added options to block input while interacting with the dash/uix
- Added a bunch of extra debugging options
- Allow users to change their desired mario audio stream volume
- Improved audio handling by using a separate thread
- Reworked input handling on gamepad and vr controllers, now includes haptics
- Added automatic static collider updates
- Improved dynamic collider registration/unregistration
- Added more output variables for mario's state (coin count, lives, health, stars, animations, etc.)
- Made mario animated on non-modded clients (using the above mentioned output variables)
- Added options to inspector to make setting up mario objects (terrain, interactables, etc.) easier (only available with DebugEnabled option)

## 1.0.0

- Initial Release
