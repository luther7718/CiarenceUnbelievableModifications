using Receiver2;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using UnityEngine.Events;
using Receiver2ModdingKit.Helpers;
using System.Collections.Generic;
using System.Reflection.Emit;
using System;
using BepInEx.Configuration;

namespace CiarenceUnbelievableModifications
{
    public static class TurretAmmoBoxBoom
    {
        public static bool verbose;
        internal static bool enabled;

		/*[HarmonyPatch(typeof(UnityHelpers), nameof(UnityHelpers.ToInt))]
		[HarmonyTranspiler]
		private static IEnumerable<CodeInstruction> TranspileToCoolToInt(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
		{
			CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);

			codeMatcher.Start();

			Debug.Log(codeMatcher.Remaining);

			codeMatcher
				.RemoveInstructions(codeMatcher.Remaining)
				.Insert(new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ret));

			codeMatcher.Print();

			return codeMatcher.InstructionEnumeration();
		}*/

		[HarmonyPatch(typeof(TurretScript), nameof(TurretScript.Damage))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TranspileDamageTurretScript(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions, generator).MatchForward(false,
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(TurretScript), nameof(TurretScript.ammo_alive))),
                new CodeMatch(OpCodes.Brfalse)
                );

            if (!codeMatcher.ReportFailure(__originalMethod, Debug.LogError))
            {
                codeMatcher
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(TurretAmmoBoxBoom), nameof(TurretAmmoBoxBoom.enabled))))
                    .InsertBranchAndAdvance(OpCodes.Brfalse_S, codeMatcher.Pos + 1)
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Component), "get_transform")))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TurretScript), nameof(TurretScript.bullets_per_belt))))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TurretScript), nameof(TurretScript.ammo_belts))))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_1))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Sub))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Mul))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TurretScript), nameof(TurretScript.bullets))))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Add))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Component), "get_transform")))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldstr, "point_pivot/gun_pivot/gun_assembly/ammo_destroy"))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Transform), nameof(Transform.Find), new[] { typeof(string) })))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(TurretScript), "cartridge_spec")))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TurretAmmoBoxBoom), nameof(FireShrapnel), new[] { typeof(Transform), typeof(int), typeof(Transform), typeof(CartridgeSpec) } )))
                    ;
            }

                return codeMatcher.InstructionEnumeration();
        }

        public static void Enable()
        {
            enabled = true;
            //ReceiverEvents.StartListening(ReceiverEventTypeGameObject.HitRobot, new UnityAction<ReceiverEventTypeGameObject, GameObject>(OnHitRobot));
        }

        public static void Disable()
        {
            enabled = false;
            //ReceiverEvents.StopListening(ReceiverEventTypeGameObject.HitRobot, OnHitRobot);
        }

        private static void OnHitRobot(ReceiverEventTypeGameObject ev, GameObject robot)
        {
            return;
            if (robot.TryGetComponent<TurretScript>(out TurretScript turret))
            {
                Debug.Log("shot robot is turret");
                if (!turret.ammo_alive)
                {
                    Debug.Log("turret's ammo box isn't alive. boom??");
                    if (turret.kds.last_shootable_query != null)
                    {
                        if (turret.kds.last_shootable_query.hit_collider.name == "ammo_box" && turret.ammo_belts > 0)
                        {
                            Debug.Log("turret's ammo box isn't alive. booming");
                            FireShrapnel(turret);
                        }
                        else
                        {
                            Debug.LogFormat("hit collider name was {0}, ammo count was {1}", turret.kds.last_shootable_query.hit_collider.name, turret.ammo_belts);
                        }
                    }
                    else
                    {
                        Debug.Log("last shootable query was null");
                    }
                }
                else
                {
                    Debug.Log("turret's ammo box is alive. sadge");
                }
            }
            else
            {
                Debug.Log("shot robot isn't turret");
            }
        }

        public static void FireShrapnel(TurretScript turret)
        {
            int bullet_count = (turret.bullets_per_belt * turret.ammo_belts); //to me, when a turret reloads, it loads an ammo belt from the ammo box to the place just behind the barrel. So if there's no ammo box left, no more ammo to explode.
            Transform shrapnel_source = turret.transform.Find("point_pivot/gun_pivot/gun_assembly/ammo_destroy");
            if (SettingsManager.Verbose) Debug.LogFormat("shrapnel_source is at {0}, transform parent is {1}", shrapnel_source.localPosition, shrapnel_source.parent.name);
            CartridgeSpec cartridge = (CartridgeSpec)typeof(TurretScript).GetField("cartridge_spec", BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).GetValue(turret);
            if (SettingsManager.Verbose) Debug.LogFormat("cartridge is: {0}", cartridge.diameter);
            //bullet_count += (turret.rifle_cocked) ? turret.bullets - 1 : turret.bullets; //ternary operator because why not, add all bullets currently "loaded" in the turret's magazine. if rifle is cocked subtract 1 round from the total
            if (SettingsManager.Verbose) Debug.LogFormat("bullet count is: {0}", bullet_count);
            for (int i = 0; i < bullet_count; i++)
            {
                Vector3 vector = LocalAimHandler.player_instance.RandomPointInCollider(1f) - shrapnel_source.transform.position;
                Vector3 vector2;
                if (i == 0 && ((vector.magnitude < 5f || Probability.Chance(0.05f) || RobotTweaks.campaign_has_override))) //targets player if magnitude between ammo box and random point in player collider is less than 5, or if unlucky, lol
                {
                    if (SettingsManager.Verbose) Debug.Log("Targeting player");
                    vector2 = vector.normalized;
                }
                else
                {
                    Vector3 onUnitSphere = UnityEngine.Random.onUnitSphere;
                    onUnitSphere.z = Mathf.Abs(onUnitSphere.z);
                    vector2 = (shrapnel_source.transform.rotation * onUnitSphere).normalized;
                }
                BulletTrajectory bulletTrajectory = BulletTrajectoryManager.PlanTrajectory(shrapnel_source.transform.position + shrapnel_source.transform.forward * 0.05f, cartridge, vector2 * UnityEngine.Random.Range(0.1f, 1f), true);
                if (BulletTrajectoryManager.draw_debug_trajectory_path)
                {
                    bulletTrajectory.draw_path = BulletTrajectory.DrawType.Debug;
                }
                else
                {
                    bulletTrajectory.draw_path = BulletTrajectory.DrawType.Tracer;
                }
                bulletTrajectory.bullet_source = turret.gameObject;
                bulletTrajectory.bullet_source_entity_type = ReceiverEntityType.Turret;
                BulletTrajectoryManager.ExecuteTrajectory(bulletTrajectory);
            }
        }

        public static void FireShrapnel(Transform turret_transform, int bullet_count, Transform shrapnel_source, CartridgeSpec cartridge)
        {
            if (SettingsManager.configTurretAmmoBoxBoom.Value == false) return;

            if (SettingsManager.Verbose) Debug.LogFormat("shrapnel_source is at {0}, transform parent is {1}", shrapnel_source.localPosition, shrapnel_source.parent.name);
            if (SettingsManager.Verbose) Debug.LogFormat("cartridge is: {0}", cartridge.diameter);
            //bullet_count += (turret.rifle_cocked) ? turret.bullets - 1 : turret.bullets; //ternary operator because why not, add all bullets currently "loaded" in the turret's magazine. if rifle is cocked subtract 1 round from the total
            if (SettingsManager.Verbose) Debug.LogFormat("bullet count is: {0}", bullet_count);
            for (int i = 0; i < bullet_count; i++)
            {
                Vector3 vector = LocalAimHandler.player_instance.RandomPointInCollider(1f) - shrapnel_source.transform.position;
                Vector3 vector2;
                if (i == 0 && ((vector.magnitude < 5f || Probability.Chance(0.05f) || RobotTweaks.campaign_has_override))) //targets player if magnitude between ammo box and random point in player collider is less than 5, or if unlucky, lol
                {
                    if (SettingsManager.Verbose) Debug.Log("Targeting player");
                    vector2 = vector.normalized;
                }
                else
                {
                    Vector3 onUnitSphere = UnityEngine.Random.onUnitSphere;
                    onUnitSphere.z = Mathf.Abs(onUnitSphere.z);
                    vector2 = (shrapnel_source.transform.rotation * onUnitSphere).normalized;
                }
                BulletTrajectory bulletTrajectory = BulletTrajectoryManager.PlanTrajectory(shrapnel_source.transform.position + shrapnel_source.transform.forward * 0.05f, cartridge, vector2 * UnityEngine.Random.Range(0.1f, 1f), true);
                if (BulletTrajectoryManager.draw_debug_trajectory_path)
                {
                    bulletTrajectory.draw_path = BulletTrajectory.DrawType.Debug;
                }
                else
                {
                    bulletTrajectory.draw_path = BulletTrajectory.DrawType.Tracer;
                }
                bulletTrajectory.bullet_source = turret_transform.gameObject;
                bulletTrajectory.bullet_source_entity_type = ReceiverEntityType.Turret;
                BulletTrajectoryManager.ExecuteTrajectory(bulletTrajectory);
            }
        }
    }
}
