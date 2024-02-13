//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEditor;
//using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;

//namespace jeanf.vrplayer
//{
//    [CustomEditor(typeof(HandsAndControllersManager))]
//    public class CreateVRHands : Editor
//    {
//        public override void OnInspectorGUI()
//        {
//            DrawDefaultInspector();

//            HandsAndControllersManager handsAndControllersManager = (HandsAndControllersManager)target;
//            if (GUILayout.Button("Create Right Controller"))
//            {
//                handsAndControllersManager.CreateRightController();
//            }

//            if (GUILayout.Button("Create Left Controller"))
//            {
//                handsAndControllersManager.CreateLeftController();
//            }
//        }
//    }
//}
