using UnityEngine;

public enum SceneName { Start, Main, Level, Loading }

public class GameScene : MonoBehaviour {
    [SerializeField] SceneName scene;
}
