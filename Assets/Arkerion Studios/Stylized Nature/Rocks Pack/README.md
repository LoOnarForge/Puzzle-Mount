ARKERION SHADERS - USAGE GUIDE

ArkerionFoliage Shader (Mesh-based)




Unity version: 2022.3 LTS (URP)
Render Pipeline: URP
Shader type: Lit (Shader Graph)

Contents
Mesh-based foliage sway shader (vertex animation)
Wind animation based on world-space noise
Supports vertex color masking (bottom → top)

Optional textures support:
Albedo (Main Tex)
Normal Map
SSS / Thickness texture

Optional vertical color gradient
Custom Subsurface Scattering (back scattering) logic
Demo scene with example foliage meshes (grass & tree leaves)

Key Features

No scripts required
Fully Shader Graph based
Artist-friendly material parameters

Works with:
simple “flat color” foliage
textured foliage (albedo + normal + SSS)
Suitable for grass, leaves, bushes, small plants

How to use

Import the provided .unitypackage
Open the demo scene (if included)
Assign the shader to a foliage material
Apply the material to a mesh
Make sure the mesh has vertex colors (see below)
Adjust wind, color and SSS parameters in the material

Vertex Color Usage (Important)

The shader uses vertex color to control wind influence:
Black (0) — no movement (base of foliage)
White (1) — full movement (tips)
A vertical gradient from bottom → top is recommended

This allows:
stable roots
moving tips
natural-looking wind animation

Texture Support (Optional)

The shader can work with or without textures.

Main Tex (Albedo)
Acts as the base color texture
Can be used alone (no gradient)
Used as the base when gradient is disabled

Normal Map
Standard tangent-space normal map
Enhances lighting and surface depth
Normal Influence controls its strength

SSS Texture (Thickness)
Grayscale texture
White = thin areas (stronger scattering)
Black = thick areas (less scattering)
Typically used for leaves

⚠️ All textures are optional.
The shader also works with flat colors only.

Color Gradient (Optional)

The shader supports an optional vertical color gradient.

Gradient Strength
0 — gradient disabled (pure albedo / Main Tex)
1 — full gradient effect

When gradient is disabled, the shader correctly falls back to Main Tex / Albedo, without darkening artifacts.

Top Color
Applied to upper parts of foliage
Usually brighter / warmer

Bottom Color
Applied to lower parts
Usually darker to ground the foliage

Subsurface Scattering (Back Scatter)

Custom back-scattering logic is implemented to simulate light passing through thin foliage.

SSS Color
Tint of the scattering light
Best results with yellow-green tones

SSS Intensity
Controls overall scattering strength
0 disables SSS completely

SSS Power
Controls the sharpness of scattering
Lower values - softer glow
Higher values - tighter highlights
Clamped to 0–5 for stability

Wind Parameters

Scale
Controls wind noise scale.
Higher values - larger, slower waves
Lower values - smaller, faster motion
Recommended: 0.5 – 2

Wind Speed (X / Y)
Controls wind direction and speed in world space.
X — world X axis
Y — world Z axis
Using different values helps avoid uniform motion.

Wind Strength
Overall movement amplitude.
Recommended: 0.05 – 0.2

Phase Strength
Controls desynchronization between instances.
Lower - synchronized motion
Higher - organic, random motion
Recommended: 0.05 – 0.3

Lighting Parameters

Normal Influence
Controls how strongly normals affect lighting.
High values can look unnatural on foliage

Smoothness
Controls surface smoothness.
Foliage looks best with very low values
Recommended range: 0 – 0.1
Higher values may cause plastic-like highlights

Notes

Shader is designed for Lit pipeline, so lighting affects shading
Bottom darkening is physically correct and comes from lighting, not color logic
Can be used for LOD-friendly foliage meshes
Suitable as a base foliage shader for production

Summary

This shader supports both:
simple stylized foliage
more realistic textured foliage with normals and SSS



––––––––––––––––––––––––––––––––––––––––––



ArkerionHardSurface (URP)

Unity version: 2022.x
 Render Pipeline: URP
 Shader type: Lit (Shader Graph)

Contents
Top-projected moss shader (world-space)
 Slope-based moss masking
 Color or texture-based moss
 Edge-softened moss blending
 Detail normal support
 Global moss override system
 Separate surface smoothness control

Description
Moss Projection Shader for Unity URP, inspired by the Unreal Engine reference material.
 Designed to add moss to upward-facing and sloped surfaces with flexible artistic control.
The shader is intended for environment assets such as rocks, trees, cliffs, stumps, and terrain props.
 It is fully material-instance driven and does not require per-object setup.

Key Features
Top-down moss projection (world-space)
 Slope-based masking (top & angle control)
 Switch between moss color and moss texture
 Edge softening for natural moss borders
 Detail normal map with tiling & offset
 Separate smoothness control for base and moss
 Optional global override (affects all materials)
 No custom scripts required for basic usage

Shader Parameters
Moss Control
MossEnabled - Enables or disables moss
 TopStrength - Overall influence of top projection
 TopSharpness - Sharpness of the top mask
 SlopeMin / SlopeMax - Controls slope range where moss appears
 EdgeSoftness - Softens moss edges to avoid hard cutoffs
Moss Appearance
MossColor - Tint color for moss
 MossTex - Optional moss texture
 MossUseTexture - Switch between color or texture
 MossTexScale - Moss texture tiling
 MossBlend - Blending strength with base material
Normal & Detail
Base Normal - Uses original model normal together with moss and detail normals
 BaseNormalStrength - Intensity of base normal before blending
 DetailNormal - Detail normal map for moss
 DetailTiling - UV tiling for detail normal
 DetailOffset - UV offset for detail normal
 DetailStrength - Intensity of detail normal
Surface Properties
BaseSmoothness - Smoothness of base material
 MossSmoothness - Smoothness of moss layer

Global Override
The shader supports global parameters, allowing you to:
Enable or disable moss globally
 Override moss color or moss texture across all materials
 Perform fast art-direction changes or seasonal variations

How to Use
Assign the shader to a Lit material
 Apply the material to environment meshes
 Adjust top projection and slope values
 Choose between moss color or moss texture
 Tune blending, normals, and smoothness as needed

Usage Notes
Works best on assets with properly oriented normals
 Moss texture is optional - color-only mode is fully supported
 Designed for URP Lit workflow
 Fully material-instance driven




––––––––––––––––––––––––––––––––––––––––––




ArkerionProceduralSkybox (URP, Unity 2022)

Unity version: 2022 LTS (URP)
Render Pipeline: URP
Shader type: Shader Graph (Custom Mesh Sky)


Contents
Custom stylized sky solution using a mesh instead of Unity Skybox
Procedural sky gradient
Procedural sun and glow
Procedural stars
Procedural animated clouds
Runtime sun synchronization via helper script

Key Features

Fully procedural (no baked skybox)
Mesh-based sky (no Unity Skybox dependency)
Artist-friendly material parameters
Performance-friendly design

Includes:
Sky Gradient
Sun
Stars
Procedural Clouds

What's Included

Sky Gradient
Procedural vertical sky gradient with customizable colors.

Sun
Procedural sun disc and glow.
Sun position is driven by the Directional Light.
Can be enabled or disabled.

Stars
Procedural star field.
Can be enabled or disabled independently.

Procedural Clouds
Fully procedural (no baked skybox).
Animated and layered for depth.
Designed to blend naturally with the sky.
Can be faded out or fully disabled via intensity control.

Runtime Sun Synchronization (Script)

The project includes a small helper script attached to a GameObject in the scene.

Purpose:
Automatically synchronizes the sky shader with the active Directional Light.
Updates sun direction in the shader in real time.

Notes:
Works in Play Mode and Edit Mode.
Requires:

* Reference to the sky material.
* A Directional Light (or RenderSettings.sun).
  No expensive per-frame operations.

Rotating the Directional Light automatically updates the sun position in the sky.

How to use

Assign the sky material to the provided mesh.
Assign a Directional Light.
Ensure the sun synchronization script is active.
Adjust visual settings via the material inspector.

Most parameters are visually driven and safe to tweak.

Technical Notes

Uses a custom curved mesh instead of Unity Skybox.
Built for URP.
Designed to work without HDR by default for better performance.
HDR can be enabled if higher color depth is required.

Performance

No volumetric rendering.
No raymarching.
No post-processing dependency.
Suitable for PC and mobile-friendly setups.

Notes

This shader was built iteratively and visually tuned.
Not every internal parameter is documented by design.
The system is meant to be artist-friendly and exploratory.

––––––––––––––––––––––––––––––––––––––––––

ArkerionStylizedWater Shader (URP)

Unity version: 2022.x
Render Pipeline: URP
Shader type: Lit (Stylized usage)

Contents
Stylized procedural water shader
Depth-based color blending
Vertex wave animation
Foam at object intersections
Fake reflections
Animated normals
Performance feature toggles

Description

Stylized Water Shader for Unity URP (Unity 2022).
Optimized for PC and Mobile.
Designed for ponds and lakes (not ocean).

The shader is fully procedural and built with future expansion in mind
(river, waterfall, underwater effects).

Key Features

Depth-based color gradient (shallow / deep colors)
Vertex waves (toggle on/off)
Foam at object intersections (depth-based)
Fake reflections (Fresnel-based, lightweight)
Animated normal map (2 layers, same texture)

Performance toggles:
Waves Enabled
Foam Enabled
Reflections Enabled

Shader Parameters

Water:
SurfaceColor
DeepColor
Distance (depth control)

Waves:
WaveSpeed
WaveAmplitude
HighFrequency
WavesEnabled

Foam:
FoamColor
FoamAmount
FoamSpeed
FoamScale
FoamEnabled

Reflections:
ReflectionColor
ReflectionStrength
ReflectionPower
ReflectionsEnabled

Normals:
NormalStrength
Normal Map (single texture, 2 animated layers)

How to use

Assign the material to a flat water mesh.
Adjust depth colors and distance for desired look.
Enable or disable Waves, Foam, and Reflections as needed.
Tune parameters depending on target platform (PC/Mobile).

Technical Details

HDR: Not required (LDR friendly)
Mobile-friendly (no real-time reflections, no SSR)

Performance Notes

No planar reflections or screen-space reflections
Fake reflections are Fresnel-based and very cheap
All heavy features can be disabled via toggles
Suitable for low-end mobile devices


