using HarmonyLib;
using System.Reflection;
using UnityModManagerNet;
using UnityEngine;

namespace Cybex
{
#if DEBUG
	[EnableReloading]
#endif
	public class MouseSmoothing
	{
		public static bool enabled = true;
		public static UnityModManager.ModEntry? mod;
		public static Settings settings = new Settings();

		static bool Load(UnityModManager.ModEntry modEntry)
		{
			mod = modEntry;
			try { settings = Settings.Load<Settings>(modEntry); } catch { }

			var harmony = new Harmony(modEntry.Info.Id);
			harmony.PatchAll(Assembly.GetExecutingAssembly());

			modEntry.OnGUI = OnGui;
			modEntry.OnSaveGUI = OnSaveGui;
			modEntry.OnToggle = OnToggle;
#if DEBUG
			modEntry.OnUnload = Unload;
#endif
			return true;
		}

		static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
		{
			if (value != enabled) enabled = value;
			return true;
		}

		static void OnGui(UnityModManager.ModEntry modEntry)
		{
			settings.Draw(modEntry);
		}

		static void OnSaveGui(UnityModManager.ModEntry modEntry)
		{
			settings.Save(modEntry);
		}

#if DEBUG
		static bool Unload(UnityModManager.ModEntry modEntry)
		{
			var harmony = new Harmony(modEntry.Info.Id);
			harmony.UnpatchAll(modEntry.Info.Id);

			return true;
		}
#endif

		static void Log (string msg)
		{
			mod?.Logger.Log($"[DV Mouse Smoothing] {msg}");
		}

		class MouseLook
		{
			static Vector2 mouseMov, mouseAcc;

			public static void LookRotation(ref CustomMouseLook __instance, Transform character, Transform camera)
			{
				if (!__instance.m_cursorIsLocked) return;

				Vector2 mouseRAW = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

				if (mouseMov.x * mouseRAW.x < 0) mouseAcc.x = 0;
				if (mouseMov.y * mouseRAW.y < 0) mouseAcc.y = 0;

				mouseMov = mouseRAW * __instance.XSensitivity;

				float dmpFac = 5 * settings.dmpMul;
				float dmpAcc = dmpFac * (Input.GetKey(KeyCode.Mouse1) ? 0.2f : 0.9f);
				float dmpBrk = dmpFac * 2 * (Input.GetKey(KeyCode.Mouse1) ? 0.5f : 0.7f);

				mouseAcc = new Vector2(
					Mathf.Lerp(mouseAcc.x, mouseMov.x, Mathf.Abs(mouseAcc.x) > Mathf.Abs(mouseMov.x) ? dmpBrk : dmpAcc * Time.deltaTime),
					Mathf.Lerp(mouseAcc.y, mouseMov.y, Mathf.Abs(mouseAcc.y) > Mathf.Abs(mouseMov.y) ? dmpBrk : dmpAcc * Time.deltaTime));

				float xMov = enabled 
					? mouseAcc.x * __instance.sensitivityMultiplier
					: Input.GetAxis("Mouse X") * __instance.XSensitivity * __instance.sensitivityMultiplier;
				float yMov = enabled 
					? mouseAcc.y * __instance.sensitivityMultiplier
					: Input.GetAxis("Mouse Y") * __instance.YSensitivity * __instance.sensitivityMultiplier;

				if (__instance.invertyMouseY) yMov *= -1;

				__instance.m_CharacterTargetRot = ((character.parent != null) ? character.localRotation : Quaternion.Euler(0f, character.eulerAngles.y, 0f));
				__instance.m_CharacterTargetRot *= Quaternion.Euler(0f, xMov, 0f);
				__instance.m_CameraTargetRot *= Quaternion.Euler(0f - yMov, 0f, 0f);
				if (__instance.clampVerticalRotation)
				{
					__instance.m_CameraTargetRot = __instance.ClampRotationAroundXAxis(__instance.m_CameraTargetRot);
				}
				character.localRotation = (__instance.cameraHolder.localRotation = __instance.m_CharacterTargetRot);
				camera.localRotation = __instance.m_CameraTargetRot * __instance.cameraAnchor.localRotation;
			}
		}

		[HarmonyPatch(typeof(CustomMouseLook), "LookRotation")]
		public static class CustomMouseLook_LookRotation_Patch
		{
			static bool Prefix (ref CustomMouseLook __instance, Transform character, Transform camera)
			{
				MouseLook.LookRotation(ref __instance, character, camera);

				return false;
				//return true;
			}
		}

		public class Settings : UnityModManager.ModSettings, IDrawable
		{
			[Draw("Hardness ", DrawType.Slider, Min = 1, Max = 10, Precision = 1)] public float dmpMul = 4;

			override public void Save(UnityModManager.ModEntry entry) { Save<Settings>(this, entry);}

			public void OnChange() { }
		}
	}
}
