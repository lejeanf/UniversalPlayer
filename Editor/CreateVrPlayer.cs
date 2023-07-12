using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.XR.CoreUtils;

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

        var mainCamera = prefab.transform.Find("CameraOffset/Main Camera");
        if (mainCamera == null) {
            Debug.LogError("Unable to find the main camera under CameraOffSet. Has it been renamed?");
            return;
        }

        var cameraComponent = mainCamera.gameObject.AddComponent<Camera>();

        prefab.GetComponent<XROrigin>().Camera = cameraComponent;

        var canvasObject = mainCamera.Find("CursorCanvas");
        if (canvasObject == null) {
            Debug.LogError("Unable to find the CursorCanvas under the main camera. Has it been renamed?");
            return;
        }
        canvasObject.GetComponent<Canvas>().worldCamera = cameraComponent;
    }
}
