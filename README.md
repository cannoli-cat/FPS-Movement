# FPS-Movement
FPS Parkour-Like movement system for unity. Highly customizable.

# Installation
- Install latest release and import .unitypackage to your project
- Add Unity Input System package

## Player Setup
- Create PlayerContainer Game Object
- Create Player within PlayerContainer game object
- Add RigidBody to Player - Set mass to 1.5 - Set to interpolate - Freeze rigidbody rotation
- Create Orientation Object as a child of the Player object
- Create Head Object as a child of the Player object, move to where you want camera to be.
- Add PlayerController.cs component. Set playerCam to the Camera Holder object. Set Orientation to the Orientation object.

## Camera Setup
- Create Camera Holder Game Object
- Add FollowCam.cs component to the Camera Holder object. Set Target to be the Head object on your Player.
- Child main camera to camera holder parent object
- Reset main camera transform

## Project Setup
- Create ground layer
- Create a physics material with no friction and put on everything you want to be ground.
- (OPTIONAL) Set Project gravity to -30
