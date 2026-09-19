using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Focused regressions; temporary objects only, no scene/save writes.</summary>
public static class MovementCombatVerification
{
    [MenuItem("Tools/Combat/Verify Movement and Target Lock")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play mode.");
        var objects = new List<GameObject>();
        Vector2 origin = new Vector2(10000, 10000);
        GameObject Make(string name, Vector2 offset, int layer)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = layer };
            objects.Add(go);
            go.transform.position = origin + offset;
            return go;
        }
        void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
        }
        try
        {
            int players = LayerMask.NameToLayer("PlayerLayer");
            int enemies = LayerMask.NameToLayer("EnemyLayer");
            Check(Physics2D.GetIgnoreLayerCollision(players, players), "Hero bodies can collide.");
            Check(Physics2D.GetIgnoreLayerCollision(players, enemies), "Opposing bodies can push.");
            foreach (float range in new[] { 0.5f, 0.83f, 0.85f, 1.5f })
            {
                foreach (float distance in new[] { 0f, 0.05f, range * 0.5f, range })
                    Check(MeleeEngagement.InAttackPosition(Vector2.zero, Vector2.right * distance, range), "Close contact rejected.");
                Check(!MeleeEngagement.InAttackPosition(Vector2.zero, Vector2.right * (range + 0.1f), range), "Out of range accepted.");
                for (int slot = 0; slot < 8; slot++)
                    Check(MeleeEngagement.InAttackPosition(MeleeEngagement.StandPoint(Vector2.zero, range, -1, slot), Vector2.zero, range), "Unreachable attack slot.");
            }

            var enemyObject = Make("Verification enemy", Vector2.zero, enemies);
            var motion = enemyObject.AddComponent<EnemyLocoMotion>();
            var enemy = enemyObject.AddComponent<EnemyManager>();
            motion.enemyRigidbody2D = enemyObject.GetComponent<Rigidbody2D>();
            motion.fairDistanceToPlayer = 2f;
            motion.playerDetectionLayer = 1 << players;
            typeof(EnemyManager).GetField("enemyLocoMotion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, motion);
            typeof(EnemyLocoMotion).GetField("enemyManager", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(motion, enemy);
            var first = Make("First hero", Vector2.right, players).AddComponent<PlayerStats>();
            first.currentHP = 100;
            first.gameObject.AddComponent<CircleCollider2D>().radius = 0.1f;
            var second = Make("Second hero", Vector2.right * 1.5f, players).AddComponent<PlayerStats>();
            second.currentHP = 100;
            second.gameObject.AddComponent<CircleCollider2D>().radius = 0.1f;
            var detect = typeof(EnemyManager).GetMethod("DetectPlayerTargets", BindingFlags.Instance | BindingFlags.NonPublic);
            Physics2D.SyncTransforms();
            detect.Invoke(enemy, null);
            Check(motion.currentTarget == first, "First nearby hero not acquired.");
            first.transform.position = origin + Vector2.right * 10;
            Physics2D.SyncTransforms();
            detect.Invoke(enemy, null);
            Check(motion.currentTarget == first, "Living lock lost beyond acquisition radius.");
            first.playerIsdead = true;
            detect.Invoke(enemy, null);
            Check(motion.currentTarget == second, "Dead target not replaced.");
            second.gameObject.SetActive(false);
            detect.Invoke(enemy, null);
            Check(motion.currentTarget == null, "Despawned target kept.");
            second.gameObject.SetActive(true);
            motion.currentTarget = second;
            second.transform.position = origin + Vector2.right * 0.05f;
            Physics2D.SyncTransforms();
            motion.HandleMoveToTarget();
            Check(motion.IsInAttackPosition() && motion.enemyRigidbody2D.bodyType == RigidbodyType2D.Kinematic, "Close enemy did not stop for combat.");

            var crowd = Make("Steering", Vector2.zero, 0).AddComponent<CrowdSeparation2D>();
            var self = Make("Walker", Vector2.up * 20, players);
            var ally = Make("Blocker", Vector2.up * 20.6f, players);
            ally.AddComponent<PlayerStats>().currentHP = 100;
            var collider = ally.AddComponent<CircleCollider2D>();
            collider.radius = 0.2f;
            Physics2D.SyncTransforms();
            Vector3 before = self.transform.position;
            Vector2 steered = crowd.SteerAroundBlockers(self.transform, Vector2.up);
            Check(Mathf.Abs(steered.x) > 0.01f && steered.y > 0.5f, "Ally not bypassed with forward progress.");
            Check(self.transform.position == before, "Steering pushed the walker.");
            ally.transform.position = self.transform.position + Vector3.down * 0.1f;
            Physics2D.SyncTransforms();
            Check(crowd.SteerAroundBlockers(self.transform, Vector2.up) == Vector2.up, "Rear overlap changed the route.");
            ally.transform.position = self.transform.position + Vector3.up * 0.6f;
            collider.isTrigger = true;
            Physics2D.SyncTransforms();
            Check(crowd.SteerAroundBlockers(self.transform, Vector2.up) == Vector2.up, "Weapon trigger treated as a blocker.");
            collider.isTrigger = false;
            ally.layer = enemies;
            Physics2D.SyncTransforms();
            Check(crowd.SteerAroundBlockers(self.transform, Vector2.up) == Vector2.up, "Opponent treated as an ally blocker.");
            Debug.Log("[Movement verification] PASS: close contact, attack slots, target acquisition/lock/death/despawn, stopping, ally steering and collision layers.");
        }
        finally
        {
            foreach (var go in objects) if (go) Object.DestroyImmediate(go);
            Physics2D.SyncTransforms();
        }
    }
}
