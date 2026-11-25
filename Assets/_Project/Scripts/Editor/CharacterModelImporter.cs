using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ShapeShifty.EditorTools
{
    // Import settings for the Blender-made characters in Art/Models/Characters (built by
    // Tools/Blender/characters.py): Unity's axes baked in, every clip looping, matte
    // materials (shiny gold), and an Animator Controller beside each model for LocomotionAnimator:
    // a blend of Idle, Walk, Trot (if it has one) and Run on "Gait" played at "PlaySpeed",
    // and Air while not "Grounded". Re-exporting a model from Blender rebuilds its controller.
    public class CharacterModelImporter : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Art/Models/Characters/";

        // Bump when changing what this does to an import, so Unity reimports the models.
        public override uint GetVersion() => 5;
        // After URP's own material set-up from the FBX, so the finish below sticks.
        public override int GetPostprocessOrder() => 10;

        static bool IsCharacter(string path) =>
            path.StartsWith(Folder) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!IsCharacter(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
        }

        void OnPreprocessAnimation()
        {
            if (!IsCharacter(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips) clip.loopTime = true; // Air is a held pose, so it loops too
            importer.clipAnimations = clips;
        }

        void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (!IsCharacter(assetPath)) return;
            // Matte, except gold (the Golden Goose), which shines.
            bool gold = material.name.StartsWith("Gold");
            material.SetFloat("_Smoothness", gold ? 0.7f : 0.1f);
            if (gold) material.SetFloat("_Metallic", 0.3f);
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
                if (IsCharacter(path)) BuildController(path);
        }

        static void BuildController(string modelPath)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            if (!clips.ContainsKey("Idle")) return;

            // Rebuilt in place rather than replaced, so prefabs keep pointing at it.
            string path = Path.ChangeExtension(modelPath, ".controller");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var old in AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>())
                AssetDatabase.RemoveObjectFromAsset(old);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter(new AnimatorControllerParameter { name = "Gait", type = AnimatorControllerParameterType.Float });
            controller.AddParameter(new AnimatorControllerParameter { name = "PlaySpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Gait",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(clips["Idle"], 0f);
            int gait = 0;
            foreach (string name in new[] { "Walk", "Trot", "Run" })
                if (clips.TryGetValue(name, out var clip)) tree.AddChild(clip, ++gait);

            var move = machine.AddState("Move");
            move.motion = tree;
            move.speedParameter = "PlaySpeed";
            move.speedParameterActive = true;
            machine.defaultState = move;

            if (clips.TryGetValue("Air", out var airClip))
            {
                var air = machine.AddState("Air");
                air.motion = airClip;
                var leave = move.AddTransition(air);
                leave.hasExitTime = false;
                leave.duration = 0.12f;
                leave.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                var land = air.AddTransition(move);
                land.hasExitTime = false;
                land.duration = 0.08f;
                land.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
        }
    }
}
