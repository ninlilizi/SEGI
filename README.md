<h1 align=center>NKLI</h1>

<p align="center">
  <img src="https://i.imgur.com/gtkjoxj.png">
</p>


# Features.
* 2D, Single Pass Rendering (Single Pass Instanced support once PP2 adds SPI)
* Runs on Unity version 2018 (2017 support is coming)
* Functions as a PostProcessing v2 effect
* Samples cones across frames. Giving a 32cone result for the cost of 2 per frame.

A fully-dynamic voxel-based global illumination system for Unity. More details at http://www.sonicether.com/segi/

<p align="center" style="display: inline-block;">
  <img height="248px" width="440px" src="https://i.imgur.com/xoR4ab6.jpg">
  <img height="248px" width="440px" src="https://i.imgur.com/m0S2k0C.jpg">
</p>

# Installation
Check the [Releases](https://github.com/ninlilizi/SEGI/releases) section above to download a version of CKGI that is a simple .unitypackage file which is ready for you to import into your project. 

You can also click the "Clone or Download" button and select "Download Zip", then extract the contents to "Assets/Plugins/SEGI" in your project to test out the latest unreleased versions of SEGI.

Install Unity Post Processing v2. (https://github.com/Unity-Technologies/PostProcessing/wiki/Installation)

Then add SEGI as a PP2 effect.  (https://github.com/Unity-Technologies/PostProcessing/wiki/Quick-start)

Add 'SEGI_Sun_Light.cs' to your main directional light

Add 'SEGI_Follow_Transform.cs' to your player character

Please refer to the User Guide.pdf for usage instructions.

# Community
If you need some help, feel free to ask any questions in the [official thread](https://forum.unity.com/threads/segi-fully-dynamic-global-illumination.410310) on Unity forums.

# Credits
* Sonic Ether for SEGI (https://github.com/sonicether/SEGI)<br>
* Cat Like Coding for FXAA (https://catlikecoding.com/unity/tutorials/advanced-rendering/fxaa/)<br>
* keijiro for Gaussian Blur(https://github.com/keijiro)
