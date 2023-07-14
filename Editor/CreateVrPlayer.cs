using UnityEngine;
using UnityEditor;
using Unity.XR.CoreUtils;

namespace jeanf.vrplayer {
    public class CreateVrPlayer : MonoBehaviour
    {
        [MenuItem("GameObject/VR Player")]
        private static void createVrPlayer() {
            GameObject prefab = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<Object>("Packages/fr.jeanf.vr.player/Runtime/Prefabs/Player.prefab"));
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

            DestroyImmediate(prefab.GetComponentInChildren<MainCameraTarget>());

            prefab.GetComponent<XROrigin>().Camera = cameraComponent;
            prefab.gameObject.GetComponentInChildren<MouseLook>().playerCamera = cameraComponent;
            mainCameraTarget.GetComponentInChildren<Canvas>().worldCamera = cameraComponent;
        }
    }
}

