using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace ChainRush.Tests.PlayMode
{
    public sealed class SourceMovementContractPlayModeTests
    {
        [Test]
        public void SourceGate_StopsAnEnemyOutsideMightyBlowBaseRadius()
        {
            var scene = SceneManager.CreateScene("SourceGateContact", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            try
            {
                var sourceGate = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Units/GateHero.prefab");
                var sourceEnemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Enemies/BugBrownSmall.prefab");
                var sourceWall = sourceGate.GetComponentsInChildren<BoxCollider2D>().Single(value => !value.isTrigger);
                var wall = new GameObject("SourceGateWall"); SceneManager.MoveGameObjectToScene(wall, scene);
                wall.layer = sourceWall.gameObject.layer;
                wall.transform.position = sourceGate.transform.InverseTransformPoint(sourceWall.transform.position);
                var box = wall.AddComponent<BoxCollider2D>(); box.size = sourceWall.size; box.offset = sourceWall.offset;
                var enemy = new GameObject("SourceEnemy"); SceneManager.MoveGameObjectToScene(enemy, scene);
                enemy.layer = sourceEnemy.layer; enemy.transform.position = new Vector3(6, 0, 0);
                var sourceBody = sourceEnemy.GetComponent<Rigidbody2D>();
                var body = enemy.AddComponent<Rigidbody2D>(); body.bodyType = sourceBody.bodyType;
                body.mass = sourceBody.mass; body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
                var circle = enemy.AddComponent<CircleCollider2D>();
                var sourceCircle = sourceEnemy.GetComponent<CircleCollider2D>();
                circle.radius = sourceCircle.radius; circle.offset = sourceCircle.offset;
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("MoreMountains.TopDownEngine.TopDownController2D"))
                    .Single(value => value != null);
                var controller = enemy.AddComponent(type);
                type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                type.GetMethod("SetMovement").Invoke(controller, new object[] { Vector3.left * 4 });
                for (int i = 0; i < 100; i++)
                {
                    type.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                    Assert.IsTrue(scene.GetPhysicsScene2D().Simulate(Time.fixedDeltaTime));
                }
                Assert.Less(body.position.x, 6, "The source controller must actually move toward the gate.");
                Assert.Greater(circle.ClosestPoint(Vector2.zero).magnitude, 2,
                    "The source gate blocks enemies outside the base MightyBlow radius of two units.");
                TestContext.WriteLine("Source gate contact: enemy=" + body.position + ", closest=" + circle.ClosestPoint(Vector2.zero));
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        [TestCase(0f)]
        [TestCase(4f)]
        public void SourceController_ComposesMovementAndMassScaledKnockback(float speed)
        {
            var scene = SceneManager.CreateScene("SourceMovementContract" + speed, new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("MoreMountains.TopDownEngine.TopDownController2D"))
                    .Single(value => value != null);
                var root = new GameObject("SourceController"); SceneManager.MoveGameObjectToScene(root, scene);
                var body = root.AddComponent<Rigidbody2D>(); body.mass = 10; body.gravityScale = 0;
                root.AddComponent<BoxCollider2D>().size = new Vector2(1.4f, 1.5f);
                var controller = root.AddComponent(type);
                type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                type.GetMethod("SetMovement").Invoke(controller, new object[] { Vector3.right * speed });
                type.GetMethod("AddForce").Invoke(controller, new object[] { Vector3.right * 50 });
                type.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                Assert.IsTrue(scene.GetPhysicsScene2D().Simulate(Time.fixedDeltaTime));
                float displacement = speed * Time.fixedDeltaTime + 50 / body.mass * Time.fixedDeltaTime * Time.fixedDeltaTime;
                Assert.That(body.position.x, Is.EqualTo(displacement).Within(.0001f),
                    "The source composes normal movement with the mass-scaled force during the same physics step.");
                body.position = Vector2.zero; body.linearVelocity = Vector2.zero;
                type.GetField("FreeMovement").SetValue(controller, false);
                type.GetMethod("AddForce").Invoke(controller, new object[] { Vector3.right * 50 });
                type.GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                Assert.IsTrue(scene.GetPhysicsScene2D().Simulate(Time.fixedDeltaTime));
                Assert.Greater(body.position.x, 0, "Without movement ownership the same body and force must produce displacement.");
            }
            finally
            {
                foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
