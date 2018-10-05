# UnityBitub

Model importer for CPIXML building information models (BIM) into Unity3D.

## Import a model
### Before 

Create a new scene and instantiate the ```PlainComplex``` prefab game object. It holds definitions which are needed by the importer.
Add following additonal tags via the Inspector pane tag list :
```
UnityBitub.Model.BuildingComponent.IS_CONSTRUCTIVE
UnityBitub.Model.BuildingComponent.IS_NON_CONSTRUCTIVE
```

### Run import

Goto ```CPI > Import CPIXML model``` and select a file. It might take a while for big models.

### How it works

CPIXML is a XML based 3D container of building component semantics and BREP data. The importer will create
a game object for each container and 3D object. All breps will be converted into meshes. Huge meshes are partioned into multiple
game objects (since Unity only allows 64K triangles per mesh).

The prefab ```PlainComplex``` holds some data for import. Most crucial: The component template, which is taken as prefab for
game object creation. By default it's references ```PlainGameObject```. Feel free to add additional data if needed.

There's a filtering list for properties attached to ```PlainComplex```. If it holds property names, only those will be added by
the importer to imported game objects (aka object3d in CPI).

### Known issues

Due to the structural layout of BIMs, the final 3D scene tree structure might become huge. Since there's no
validation of orientation at importing time, (partial) meshes may become invalid or are flipped inside out (wrong orientation).
The outcome strongly depends on the originating export (some do better - some worse). UV coordinates are computed by the importer (not part of CPI). 
If triangles are close to be wedges, numerical instabilities will cause visual artifacts on the shader side. I'm open for more stable approaches ...  

## Options

- Show / Hide openings
- Show / Hide spaces
- Add some rigid bodies and colliders for real gaming experiences.
- Turn on gravity (make sure you have a world ground plane ;-) and a well equipped machine at hand)


