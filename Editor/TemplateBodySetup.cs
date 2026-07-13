using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// One-shot setup for the packaged template body: switches the character and the
    /// Mixamo clips to Humanoid (so they retarget onto each other), enables looping on
    /// the locomotion clips, and rebuilds TemplateCharacter.controller with the
    /// FirstPersonBody parameter contract (Speed, NormalizedSpeed, MoveX, MoveY,
    /// IsSprinting, CrouchBlend, IsGrounded, IsSeated), a 2D locomotion blend tree,
    /// an airborne state and a seated state. Safe to run again after adding clips.
    /// </summary>
    public static class TemplateBodySetup
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [MenuItem("Tools/UniversalPlayer/Setup Template Body (import + controller)")]
        public static void Run()
        {
            var controllerPath = FindAssetPath("TemplateCharacter t:AnimatorController");
            if (controllerPath == null)
            {
                Debug.LogError($"{LogPrefix} TemplateBodySetup: TemplateCharacter.controller not found — was the TemplateCharacter folder moved or renamed?");
                return;
            }
            var animationsDir = Path.GetDirectoryName(controllerPath)!.Replace('\\', '/');
            var packDir = $"{animationsDir}/Mixamo_Anim_female_pack";

            // ---- 1. import settings: humanoid everywhere, loops on locomotion clips ----
            var characterFbxPath = FindAssetPath("TemplateCharacter t:Model");
            if (characterFbxPath != null) MakeHumanoid(characterFbxPath, configureClips: false);
            else Debug.LogWarning($"{LogPrefix} TemplateBodySetup: TemplateCharacter model FBX not found — skipped its rig setup.");

            var clipPaths = AssetDatabase.FindAssets("t:Model", new[] { packDir })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            if (clipPaths.Count == 0)
            {
                Debug.LogError($"{LogPrefix} TemplateBodySetup: no animation FBX found under '{packDir}'.");
                return;
            }
            foreach (var path in clipPaths) MakeHumanoid(path, configureClips: true);

            // ---- 2. rebuild the controller ----
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var fastestCycle = RebuildController(controller, clipPaths);

            // ---- 3. make the template character the player's default body ----
            EnsureBodyWrapperAndPlayerWiring(controller, fastestCycle);

            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix} TemplateBodySetup: done — {clipPaths.Count} clips humanoid+looped, controller rebuilt " +
                $"({controller.parameters.Length} parameters, {controller.layers[0].stateMachine.states.Length} states). " +
                "Enter play mode in M&K mode and walk around.");
        }

        /// <summary>
        /// Guarantees TemplateCharacterBody.prefab is a valid wrapper (own root + nested
        /// character with an Animator) and that the Player prefab's FirstPersonBody uses
        /// it as the default body with the packaged controller.
        /// </summary>
        private static void EnsureBodyWrapperAndPlayerWiring(AnimatorController controller, float fastestCycleSpeed)
        {
            var characterPrefabPath = AssetDatabase.FindAssets("TemplateCharacter t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileName(p) == "TemplateCharacter.prefab");
            if (characterPrefabPath == null)
            {
                Debug.LogError($"{LogPrefix} TemplateBodySetup: TemplateCharacter.prefab not found — player body wiring skipped.");
                return;
            }

            var wrapperPath = $"{Path.GetDirectoryName(characterPrefabPath)!.Replace('\\', '/')}/TemplateCharacterBody.prefab";
            var wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPath);
            if (wrapper == null || wrapper.GetComponentInChildren<Animator>(true) == null)
            {
                var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(characterPrefabPath);
                var root = new GameObject("TemplateCharacterBody");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
                instance.transform.SetParent(root.transform, false);
                wrapper = PrefabUtility.SaveAsPrefabAsset(root, wrapperPath); // overwriting keeps the GUID
                Object.DestroyImmediate(root);
                Debug.Log($"{LogPrefix} TemplateBodySetup: rebuilt '{wrapperPath}' (was missing or had no Animator).");
            }

            var playerPrefabPath = AssetDatabase.FindAssets("Player t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.Contains("UniversalPlayer") && Path.GetFileName(p) == "Player.prefab");
            if (playerPrefabPath == null)
            {
                Debug.LogWarning($"{LogPrefix} TemplateBodySetup: Player.prefab not found — assign the wrapper to " +
                    "FirstPersonBody.bodyPrefab on your player manually.");
                return;
            }

            using var scope = new PrefabUtility.EditPrefabContentsScope(playerPrefabPath);
            var body = scope.prefabContentsRoot.GetComponentInChildren<FirstPersonBody>(true);
            if (body == null)
            {
                Debug.LogWarning($"{LogPrefix} TemplateBodySetup: no FirstPersonBody on '{playerPrefabPath}' — " +
                    "is the Body node missing from the prefab?");
                return;
            }
            var serialized = new SerializedObject(body);
            serialized.FindProperty("bodyPrefab").objectReferenceValue = wrapper;
            serialized.FindProperty("bodyController").objectReferenceValue = controller;
            if (fastestCycleSpeed > 0f)
                serialized.FindProperty("fastestCycleGroundSpeed").floatValue = fastestCycleSpeed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string FindAssetPath(string filter)
        {
            return AssetDatabase.FindAssets(filter)
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.Contains("TemplateCharacter"));
        }

        private static void MakeHumanoid(string path, bool configureClips)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            var dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }

            if (configureClips)
            {
                var clips = importer.clipAnimations is { Length: > 0 } ? importer.clipAnimations : importer.defaultClipAnimations;
                var fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                // One-shot clips; everything else is a locomotion/idle cycle and loops.
                var oneShot = fileName is "jump" or "falling to landing" or "sitting" or "stand up"
                    or "button pushing" or "picking up object" or "opening door inwards";
                foreach (var clip in clips)
                {
                    clip.loopTime = !oneShot;
                    // Mixamo clips TRAVEL. Bake rotation, height AND XZ into the pose so the
                    // cycles play fully in place — the CharacterController owns all movement,
                    // and un-baked root motion made the body slide away from the player.
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = false;
                }
                importer.clipAnimations = clips;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        /// <summary>Rebuilds the controller; returns the fastest forward cycle's ground speed (m/s) for the MotionSpeed drive.</summary>
        private static float RebuildController(AnimatorController controller, List<string> clipPaths)
        {
            var fastestCycleSpeed = 0f;
            AnimationClip Clip(string nameContains)
            {
                var path = clipPaths.FirstOrDefault(p =>
                    Path.GetFileNameWithoutExtension(p).ToLowerInvariant() == nameContains) ?? clipPaths.FirstOrDefault(p =>
                    Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(nameContains));
                if (path == null) return null;
                return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            }

            var idle = Clip("idle");
            var walk = Clip("walking");
            var run = Clip("running");
            var back = Clip("walking_backwards");
            var runBack = Clip("run backward");
            var strafeWalkL = Clip("left strafe walk");
            var strafeWalkR = Clip("right strafe walk");
            var jump = Clip("jump");
            var fallingIdle = Clip("falling idle");
            var landing = Clip("falling to landing");
            var sitDown = Clip("sitting");         // exact-name match wins over "sitting idle"
            var seatedIdle = Clip("sitting idle");
            var standUp = Clip("stand up");
            var crouchIdle = Clip("crouching idle");
            var crouchWalk = Clip("crouched walking");
            if (idle == null || walk == null)
            {
                Debug.LogError($"{LogPrefix} TemplateBodySetup: idle/walking clips not found in the pack — controller not rebuilt.");
                return fastestCycleSpeed;
            }

            // Wipe previous content but keep the controller asset (its GUID is referenced by the prefab).
            foreach (var layer in controller.layers.ToList()) controller.RemoveLayer(0);
            foreach (var parameter in controller.parameters.ToList()) controller.RemoveParameter(parameter);
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller))
                         .Where(a => a != controller && a != null))
            {
                Object.DestroyImmediate(sub, true);
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("NormalizedSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("CrouchBlend", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsSeated", AnimatorControllerParameterType.Bool);
            controller.AddParameter("MotionSpeed", AnimatorControllerParameterType.Float);
            // MotionSpeed defaults to 1 so states play normally when nothing drives it.
            var parameters = controller.parameters;
            parameters[parameters.Length - 1].defaultFloat = 1f;
            controller.parameters = parameters;

            controller.AddLayer("Locomotion");
            var stateMachine = controller.layers[0].stateMachine;

            // MoveX/MoveY are local planar velocity in REAL m/s and every anchor sits at
            // its clip's natural ground speed (measured from the root motion), so the feet
            // match the floor at any velocity in between. Past the fastest cycle the
            // MotionSpeed state multiplier scales playback instead.
            float GroundSpeed(AnimationClip clip, float fallback)
            {
                if (clip == null) return fallback;
                var speed = new Vector2(clip.averageSpeed.x, clip.averageSpeed.z).magnitude;
                return speed > 0.2f ? speed : fallback;
            }

            var walkSpeed = GroundSpeed(walk, 1.4f);
            var runSpeed = GroundSpeed(run, 3f);
            var blendTree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY",
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.AddChild(idle, Vector2.zero);
            blendTree.AddChild(walk, new Vector2(0f, walkSpeed));
            if (run != null) blendTree.AddChild(run, new Vector2(0f, runSpeed));
            if (back != null) blendTree.AddChild(back, new Vector2(0f, -GroundSpeed(back, 1.2f)));
            if (runBack != null) blendTree.AddChild(runBack, new Vector2(0f, -GroundSpeed(runBack, 2.5f)));
            if (strafeWalkL != null) blendTree.AddChild(strafeWalkL, new Vector2(-GroundSpeed(strafeWalkL, 1.2f), 0f));
            if (strafeWalkR != null) blendTree.AddChild(strafeWalkR, new Vector2(GroundSpeed(strafeWalkR, 1.2f), 0f));

            var locomotion = stateMachine.AddState("Locomotion");
            locomotion.motion = blendTree;
            locomotion.speedParameterActive = true;
            locomotion.speedParameter = "MotionSpeed";
            stateMachine.defaultState = locomotion;
            fastestCycleSpeed = Mathf.Max(walkSpeed, runSpeed);

            // ---- crouch (CrouchBlend crosses 0.5 both ways) ----
            if (crouchIdle != null)
            {
                var crouchTree = new BlendTree
                {
                    name = "Crouched",
                    blendType = BlendTreeType.FreeformCartesian2D,
                    blendParameter = "MoveX",
                    blendParameterY = "MoveY",
                    hideFlags = HideFlags.HideInHierarchy,
                };
                AssetDatabase.AddObjectToAsset(crouchTree, controller);
                crouchTree.AddChild(crouchIdle, Vector2.zero);
                if (crouchWalk != null)
                {
                    // No crouched strafes/backwards in the pack: reuse the walk cycle in
                    // every direction so movement never snaps back to the idle pose.
                    var crouchSpeed = GroundSpeed(crouchWalk, 1f);
                    crouchTree.AddChild(crouchWalk, new Vector2(0f, crouchSpeed));
                    crouchTree.AddChild(crouchWalk, new Vector2(0f, -crouchSpeed));
                    crouchTree.AddChild(crouchWalk, new Vector2(-crouchSpeed, 0f));
                    crouchTree.AddChild(crouchWalk, new Vector2(crouchSpeed, 0f));
                }

                var crouched = stateMachine.AddState("Crouched");
                crouched.motion = crouchTree;
                crouched.speedParameterActive = true;
                crouched.speedParameter = "MotionSpeed";
                var toCrouch = locomotion.AddTransition(crouched);
                toCrouch.AddCondition(AnimatorConditionMode.Greater, 0.5f, "CrouchBlend");
                toCrouch.hasExitTime = false;
                toCrouch.duration = 0.25f;
                var fromCrouch = crouched.AddTransition(locomotion);
                fromCrouch.AddCondition(AnimatorConditionMode.Less, 0.5f, "CrouchBlend");
                fromCrouch.hasExitTime = false;
                fromCrouch.duration = 0.25f;
            }

            // ---- sitting (sit-down clip -> seated idle -> stand-up clip) ----
            if (seatedIdle != null || sitDown != null)
            {
                var seated = stateMachine.AddState("Seated");
                seated.motion = seatedIdle != null ? seatedIdle : idle;

                var entryState = seated;
                // A body that is actually MOVING must never show a sitting pose, no matter
                // which flag desynced: physical speed always wins.
                SpeedEscape(seated, locomotion);
                if (sitDown != null)
                {
                    var sittingDown = stateMachine.AddState("SittingDown");
                    sittingDown.motion = sitDown;
                    sittingDown.speed = 1.4f; // Mixamo ceremonies are theatrical — tighten them
                    sittingDown.speedParameterActive = true;
                    sittingDown.speedParameter = "MotionSpeed";
                    var settle = sittingDown.AddTransition(seated);
                    settle.hasExitTime = true;
                    settle.exitTime = 0.9f;
                    settle.duration = 0.1f;
                    // Stood up again before fully seated: bail out immediately.
                    EscapeTransition(sittingDown, locomotion, "IsSeated", whenTrue: false);
                    SpeedEscape(sittingDown, locomotion);
                    entryState = sittingDown;
                }
                var toSeated = locomotion.AddTransition(entryState);
                toSeated.AddCondition(AnimatorConditionMode.If, 0f, "IsSeated");
                toSeated.hasExitTime = false;
                toSeated.duration = 0.15f;

                if (standUp != null)
                {
                    var standingUp = stateMachine.AddState("StandingUp");
                    standingUp.motion = standUp;
                    standingUp.speed = 1.4f;
                    standingUp.speedParameterActive = true;
                    standingUp.speedParameter = "MotionSpeed";
                    var rise = seated.AddTransition(standingUp);
                    rise.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSeated");
                    rise.hasExitTime = false;
                    rise.duration = 0.1f;
                    var done = standingUp.AddTransition(locomotion);
                    done.hasExitTime = true;
                    done.exitTime = 0.6f;
                    done.duration = 0.2f;
                    // The player is already running away — never keep playing the ceremony.
                    SpeedEscape(standingUp, locomotion);
                }
                else
                {
                    var fromSeated = seated.AddTransition(locomotion);
                    fromSeated.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSeated");
                    fromSeated.hasExitTime = false;
                    fromSeated.duration = 0.25f;
                }
            }

            // ---- airborne (jump takeoff -> falling loop -> landing) ----
            if (jump != null)
            {
                var airborne = stateMachine.AddState("Airborne");
                airborne.motion = jump;
                var toAir = locomotion.AddTransition(airborne);
                toAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
                toAir.hasExitTime = false;
                toAir.duration = 0.1f;

                var landingTarget = locomotion;
                if (landing != null)
                {
                    var landingState = stateMachine.AddState("Landing");
                    landingState.motion = landing;
                    landingState.speed = 1.3f;
                    var recover = landingState.AddTransition(locomotion);
                    recover.hasExitTime = true;
                    recover.exitTime = 0.55f;
                    recover.duration = 0.2f;
                    // Landing into a run must not lock the feet for the whole recover clip.
                    SpeedEscape(landingState, locomotion);
                    landingTarget = landingState;
                }

                if (fallingIdle != null)
                {
                    var falling = stateMachine.AddState("Falling");
                    falling.motion = fallingIdle;
                    var takeoffDone = airborne.AddTransition(falling);
                    takeoffDone.hasExitTime = true;
                    takeoffDone.exitTime = 0.7f;
                    takeoffDone.duration = 0.25f;
                    var fallToLand = falling.AddTransition(landingTarget);
                    fallToLand.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
                    fallToLand.hasExitTime = false;
                    fallToLand.duration = 0.1f;
                }
                var jumpToLand = airborne.AddTransition(landingTarget);
                jumpToLand.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
                jumpToLand.hasExitTime = false;
                jumpToLand.duration = 0.1f;
            }

            EditorUtility.SetDirty(controller);
            return fastestCycleSpeed;
        }

        /// <summary>Immediate exit from a one-shot state when the player actually moves.</summary>
        private static void SpeedEscape(AnimatorState from, AnimatorState to)
        {
            var escape = from.AddTransition(to);
            escape.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");
            escape.hasExitTime = false;
            escape.duration = 0.15f;
        }

        private static void EscapeTransition(AnimatorState from, AnimatorState to, string boolParameter, bool whenTrue)
        {
            var escape = from.AddTransition(to);
            escape.AddCondition(whenTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, boolParameter);
            escape.hasExitTime = false;
            escape.duration = 0.15f;
        }
    }
}
