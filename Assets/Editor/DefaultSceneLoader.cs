#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoadAttribute]
public class DefaultSceneLoader : MonoBehaviour {
    const string playMain = "Game/Play Game";

    static bool playMainScene {
        get { return EditorPrefs.HasKey(playMain) && EditorPrefs.GetBool(playMain); }
        set { EditorPrefs.SetBool(playMain, value); }
    }

    [MenuItem(playMain)]
    static void PlayMain() {
        playMainScene = !playMainScene;
        UnityEditor.Menu.SetChecked(playMain, playMainScene);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadDefaultScene(){
        if (playMainScene)
            SceneManager.LoadScene(SceneName.Loading.ToString());
    }
}
#endif