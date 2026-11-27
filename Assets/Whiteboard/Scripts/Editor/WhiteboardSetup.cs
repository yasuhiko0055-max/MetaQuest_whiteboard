#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestWhiteboard.EditorTools
{
    /// <summary>
    /// メニュー「Whiteboard > シーンにボードを配置」で、今開いているシーンにボードとペンを一式置く。
    /// 先に Meta の Building Blocks で「Camera Rig」と「Passthrough」を置いておくこと。
    /// </summary>
    public static class WhiteboardSetup
    {
        const string MatDir = "Assets/Whiteboard/Materials";

        [MenuItem("Whiteboard/シーンにボードを配置")]
        public static void Setup()
        {
            var rig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig == null)
            {
                EditorUtility.DisplayDialog("Whiteboard",
                    "シーンに Camera Rig が見つかりません。\n\nメニュー Meta > Tools > Building Blocks から「Camera Rig」と「Passthrough」をシーンに追加してから、もう一度実行してください。",
                    "OK");
                return;
            }

            var (boardMat, lineMat) = EnsureMaterials();

            var existing = Object.FindFirstObjectByType<WhiteboardBoard>();
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var boardGo = new GameObject("Whiteboard");
            Undo.RegisterCreatedObjectUndo(boardGo, "Create Whiteboard");
            boardGo.transform.position = new Vector3(0f, 1.4f, 1.8f);
            var board = boardGo.AddComponent<WhiteboardBoard>();
            board.boardMaterial = boardMat;
            board.lineMaterial = lineMat;

            var grabber = boardGo.AddComponent<BoardGrabber>();
            grabber.leftHand = rig.leftControllerAnchor;
            grabber.rightHand = rig.rightControllerAnchor;
            grabber.head = rig.centerEyeAnchor;

            AddPen(rig.rightControllerAnchor, OVRInput.Controller.RTouch, board);
            AddPen(rig.leftControllerAnchor, OVRInput.Controller.LTouch, board);

            EditorSceneManager.MarkSceneDirty(boardGo.scene);
            Selection.activeGameObject = boardGo;

            string msg = "ボードと左右のペンを配置しました。";
            if (Object.FindFirstObjectByType<OVRPassthroughLayer>() == null)
                msg += "\n\n※ Passthrough が見つかりません。教室を見ながら書くには Building Blocks の「Passthrough」も追加してください。";
            EditorUtility.DisplayDialog("Whiteboard", msg, "OK");
        }

        static void AddPen(Transform anchor, OVRInput.Controller c, WhiteboardBoard board)
        {
            if (anchor == null) return;
            var old = anchor.GetComponent<PenController>();
            if (old != null) Undo.DestroyObjectImmediate(old);
            var pen = Undo.AddComponent<PenController>(anchor.gameObject);
            pen.controller = c;
            pen.board = board;
        }

        static (Material board, Material line) EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
                AssetDatabase.CreateFolder("Assets/Whiteboard", "Materials");

            bool urp = GraphicsSettings.currentRenderPipeline != null;
            Shader boardShader = Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Texture");
            Shader lineShader = Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Sprites/Default");

            var boardMat = LoadOrCreate($"{MatDir}/WhiteboardSurface.mat", boardShader);
            var lineMat = LoadOrCreate($"{MatDir}/WhiteboardInk.mat", lineShader);
            if (lineMat.HasProperty("_Cull")) lineMat.SetFloat("_Cull", 0f); // 裏表どちらからも見えるように
            lineMat.doubleSidedGI = true;
            EditorUtility.SetDirty(lineMat);
            AssetDatabase.SaveAssets();
            return (boardMat, lineMat);
        }

        static Material LoadOrCreate(string path, Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null)
            {
                if (shader != null && m.shader != shader) m.shader = shader;
                return m;
            }
            m = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
#endif
