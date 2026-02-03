using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using MoreMountains.Tools;
using UnityEngine;

public class EnemyRemover : MonoBehaviour {
    void OnTriggerStay2D(Collider2D other) {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null)
            enemy.Kill();
    }
}
