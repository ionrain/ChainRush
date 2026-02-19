using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public enum SceneLoaderFunction { OnAwake, OnStart, OnTrigger }

[System.Serializable]
public class SceneLoadProgressEvent : UnityEvent<float> { }

public class SceneLoader : MonoBehaviour {
    [SerializeField] SceneName scene;
    [SerializeField] SceneLoaderFunction use = SceneLoaderFunction.OnTrigger;
    [SerializeField] LoadSceneMode mode;
    
    [Header("Asynchronious Load")]
    [SerializeField] bool asynchronious;
    [SerializeField] SceneLoadProgressEvent OnLoadProgress;

    void Start() {
        if (use == SceneLoaderFunction.OnStart)
            LoadScene();
    }

    void Awake() {
        if (use == SceneLoaderFunction.OnAwake)
            LoadScene();
    }

    public void LoadScene() {
        LoadScene(scene);
    }

    public void LoadScene(SceneName sceneName) {
        if (asynchronious)
            StartCoroutine(LoadSceneCo());
        else
            SceneManager.LoadScene(sceneName.ToString(), mode);
    }

    public void LoadScene(SceneName sceneName, LoadSceneMode loadMode) {
        SceneManager.LoadScene(sceneName.ToString(), loadMode);
    }

    protected IEnumerator LoadSceneCo() {
        yield return null;
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(scene.ToString(), mode);
        float progress = 0;
        while (!loadOperation.isDone) {
            if (progress <= loadOperation.progress) {
                progress = loadOperation.progress;
                OnLoadProgress?.Invoke(progress);
            }
            yield return null;
        }
    }
}
