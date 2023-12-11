using UnityEngine;
using UnityEditor;
using Unity.XR.CoreUtils;

namespace jeanf.vrplayer {
    public class CreateVrPlayer : MonoBehaviour
    {
        [MenuItem("GameObject/Create Universal Player")]
        private static void createVrPlayer()
        {
            var playerInPackage = AssetDatabase.LoadAssetAtPath<Object>("Packages/fr.jeanf.vr.player/Runtime/Prefabs/Player.prefab");
            var playerInPackageBuilder = AssetDatabase.LoadAssetAtPath<Object>("Assets/VR_Player/Runtime/Prefabs/Player.prefab");
            var playerPrefab = playerInPackage == null ? playerInPackageBuilder : playerInPackage;
            var prefab = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            if(Selection.activeTransform != null)
            {
                prefab.transform.SetParent(Selection.activeTransform, false);
            }
            prefab.transform.localPosition = Vector3.zero;
            prefab.transform.localEulerAngles = Vector3.zero;
            prefab.transform.localScale = Vector3.one;

            // The camera component is not on the prefab since it automatically adds scripts
            // based on the render pipeline. By adding it on creation, it will automatically
            // add the required components for the current render pipeline.

            var mainCameraTarget = prefab.GetComponentInChildren<MainCameraTarget>().gameObject;
            var cameraComponent = mainCameraTarget.AddComponent<Camera>();
            cameraComponent.nearClipPlane = 0.1f;

            var mouseLook = prefab.GetComponentInChildren<MouseLook>();
            mouseLook.playerCamera = cameraComponent;


            prefab.GetComponent<XROrigin>().Camera = cameraComponent;
            prefab.gameObject.GetComponentInChildren<MouseLook>().playerCamera = cameraComponent;
            mainCameraTarget.GetComponentInChildren<Canvas>().worldCamera = cameraComponent;
        }
    }
}

