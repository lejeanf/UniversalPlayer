UNIVERSAL PLAYER


Description:
- this player intends to extend the basic player from unity xri 2.3.1

------------------------------------------------------------------------------------------------------
Features:
- Teleportation through event (using scriptable objects)
- Hand posing
- Swap gender on hand using a slider
- Swap skin color
- Add gloves to hand using events
- Mouse and keyboard works as well as VR (all headset) although there is sometimes a bug when returning ot MK after using VR.


------------------------------------------------------------------------------------------------------
Compatibility:
- URP
- HDRP
- Unity 2022.3.3f1 and above.


------------------------------------------------------------------------------------------------------
How to install the package:
- add new scopedRegisteries in ProjectSettings/Package manager
- name: jeanf
- url: https://registry.npmjs.com
- scope fr.jeanf

------------------------------------------------------------------------------------------------------
------------------------------------------------------------------------------------------------------
How to install the VRPlayer for edition:
- Add new scopedRegisteries in ProjectSettings/Package manager (see: "How to install the package" section)
- Install following packages from the scoped registry:
    - Event System
    - Property Drawer
- Clone the git repository in Project/Assets folder. 
- Install required packages:
    - Lit Motion (link: https://github.com/AnnulusGames/LitMotion) 
    - Vector Graphics (link: https://docs.unity3d.com/Packages/com.unity.vectorgraphics@2.0/manual/index.html for installation guidelines)
    - Input System (You will also need to edit your project settings in Edit/projectSettings/Player/Configuration/ActiveInputHandling and turn on new Input System Package)
    - XR Core Utilities
    - XR Hands (with Hands Visualizer sample)
    - XR Interaction Toolkit (with Starter Assets, XR Device Simulator, Tunneling Vignette and Hands Interaction Demo samples)
    - XR Legacy Input Helpers
    - XR Plugin Management
    - OpenXR Plugin
    - Occulus XR Plugin
    - VR
    

------------------------------------------------------------------------------------------------------
How to get started:
- In a scene project's hiearchy: Right click > Create VR Player . This will create a player for your current rendering pipeline and add and bind the camera to the necessary scripts.
- It might be a good idea to make local "events" and bind it tho the scripts as to avoid issues with furute updates if you modify the player, for this you can just copy the events in the pacakge to your local project.


------------------------------------------------------------------------------------------------------
Licence:

<a rel="license" href="http://creativecommons.org/licenses/by-nc-sa/4.0/"><img alt="Licence Creative Commons" style="border-width:0" src="https://i.creativecommons.org/l/by-nc-sa/4.0/88x31.png" /></a><br />Ce(tte) œuvre est mise à disposition selon les termes de la <a rel="license" href="http://creativecommons.org/licenses/by-nc-sa/4.0/">Licence Creative Commons Attribution - Pas d’Utilisation Commerciale - Partage dans les Mêmes Conditions 4.0 International</a>.

This license lets others remix, adapt, and build upon your work non-commercially, as long as they credit you and license their new creations under the identical terms.

------------------------------------------------------------------------------------------------------
Credits:

- This repo was started during a artist residency called fantomas that happended at <a href="https://www.medrar.org/">Medrar</a> in Cairo From October to December 2022. The aim of this workshop was to teach artists create artworks in VR.
- Since then this repo is being supported by a research group based in Canada at UQAR univeristy : Laboratoire Onirique and more specifically by the project UVS (Unitée virtuelle de soins = Virtual Health Unit) initiated by Daniel Milhomme and Frédrérique Banville
- [3D] <a href="https://www.linkedin.com/in/jonathan-l%C3%A9pinay/?originalSubdomain=ca">Jonathan Lepinay</a>
- [Code] Nicolas Chouin & <a href="https://jeanfrancoisrobin.art">Jean-François Robin</a>
- partly inspired by: https://github.com/UnityTechnologies/open-project-1/tree/devlogs/2-scriptable-objects for the event system
