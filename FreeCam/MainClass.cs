using FreeCam.Components;
using HarmonyLib;
using OWML.Common;
using OWML.Common.Enums;
using OWML.ModHelper;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using static InputConsts;

namespace FreeCam;

class MainClass : ModBehaviour
{
	private GameObject _freeCam;
	private Camera _camera;
	private OWCamera _owCamera;

	public static UnityEvent OnFreeCamEntered = new();
	public static UnityEvent OnFreeCamExited = new();

	public static bool InFreeCam { get; private set; }

	public static bool ShowPrompts { get; private set; }
	public static bool ShowTogglePrompt { get; private set; }
	public static bool ResetParent { get; private set; }

	public static InputCommandType ToggleFreeCamBindType { get; private set; }
	public static InputCommandType ChangeSpeedBindType { get; private set; }
	public static InputCommandType CameraResetBindType { get; private set; }
	public static InputCommandType FlashlightRangeBindType { get; private set; }
	public static InputCommandType ToggleHUDBindType { get; private set; }
	public static InputCommandType TeleportBindType { get; private set; }
	public static InputCommandType ReparentBindType { get; private set; }
	public static InputCommandType CenterOnPlayerBindType { get; private set; }
	public static InputCommandType FlashlightSpeedBindType { get; private set; }
	public static InputCommandType Time0BindType { get; private set; }
	public static InputCommandType Time50BindType { get; private set; }
	public static InputCommandType Time100BindType { get; private set; }

	public static IInputCommands ToggleFreeCamBind => InputLibrary.GetInputCommand(ToggleFreeCamBindType);
	public static IInputCommands ToggleHUDBind => InputLibrary.GetInputCommand(ToggleHUDBindType);
	public static IInputCommands ChangeSpeedBind => InputLibrary.GetInputCommand(ChangeSpeedBindType);
	public static IInputCommands CameraResetBind => InputLibrary.GetInputCommand(CameraResetBindType);
	public static IInputCommands FlashlightRangeBind => InputLibrary.GetInputCommand(FlashlightRangeBindType);
	public static IInputCommands TeleportBind => InputLibrary.GetInputCommand(TeleportBindType);
	public static IInputCommands ReparentBind => InputLibrary.GetInputCommand(ReparentBindType);
	public static IInputCommands CenterOnPlayerBind => InputLibrary.GetInputCommand(CenterOnPlayerBindType);
	public static IInputCommands FlashlightSpeedBind => InputLibrary.GetInputCommand(FlashlightSpeedBindType);
	public static IInputCommands Time0Bind => InputLibrary.GetInputCommand(Time0BindType);
	public static IInputCommands Time50Bind => InputLibrary.GetInputCommand(Time50BindType);
	public static IInputCommands Time100Bind => InputLibrary.GetInputCommand(Time100BindType);

	public static readonly System.Collections.Generic.Dictionary<AstroObject.Name, InputCommandType> CenterOnPlanetBindTypes = new();

	public static IInputCommands GetCenterOnPlanetBind(AstroObject.Name name)
	{
		if (CenterOnPlanetBindTypes.TryGetValue(name, out var bindType))
		{
			return InputLibrary.GetInputCommand(bindType);
		}
		return null;
	}

	private InputMode _storedMode;
	private int _fov;
	private int _nearClipPlane;
	private ICommonCameraAPI _commonCameraAPI;
	private GameObject _hud;
	private static MainClass _instance;

	public void Start()
	{
		Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

		_instance = this;

		try
		{
			_commonCameraAPI = ModHelper.Interaction.TryGetModApi<ICommonCameraAPI>("xen.CommonCameraUtility");
		}
		catch (Exception e)
		{
			WriteError($"{e}");
		}
		finally
		{
			if (_commonCameraAPI == null)
			{
				WriteError($"未找到 CommonCameraAPI。FreeCam 将无法运行");
				enabled = false;
			}
		}

		// Toggles
		ToggleFreeCamBindType = ModHelper.RebindingHelper.RegisterRebindable(
            "切换自由相机",
            "用于开启/关闭自由相机的按键",
			Key.Semicolon,
			GamepadBinding.None,
			false
		);
		ToggleHUDBindType = ModHelper.RebindingHelper.RegisterRebindable(
            "切换 HUD",
            "在自由相机模式下切换头盔 HUD 的显示",
			Key.Quote,
			GamepadBinding.None,
			false
		);

		// Camera
		ChangeSpeedBindType = ModHelper.RebindingHelper.RegisterRebindable(
            "调整移动速度",
            "用于增加/减少移动速度的轴。",
			MouseBinding.ScrollUp,
			GamepadBinding.DPadUp,
			MouseBinding.ScrollDown,
			GamepadBinding.DPadDown,
			true
		);
		CameraResetBindType = ModHelper.RebindingHelper.RegisterRebindable(
            "重置相机",
            "将相机重置到默认位置",
			Key.DownArrow,
			GamepadBinding.None,
			false
		);

		// Flashlight
		FlashlightRangeBindType = ModHelper.RebindingHelper.RegisterRebindable(
            "手电筒范围",
			"",
			Key.LeftBracket,
			GamepadBinding.DPadLeft,
			Key.RightBracket,
			GamepadBinding.DPadRight,
			true
		);
		FlashlightSpeedBindType = ModHelper.RebindingHelper.RegisterRebindable(
            "手电筒快速调节",
            "按住时以更快的速度调节手电筒范围",
			Key.LeftShift,
			GamepadBinding.None,
			false
		);

		// Time shortcuts
		Time0BindType = ModHelper.RebindingHelper.RegisterRebindable(
			"Time 0%",
            "将游戏时间流速设为 0%",
			Key.Comma,
			GamepadBinding.None,
			false
		);
		Time50BindType = ModHelper.RebindingHelper.RegisterRebindable(
			"Time 50%",
            "将游戏时间流速设为 50%",
			Key.Period,
			GamepadBinding.None,
			false
		);
		Time100BindType = ModHelper.RebindingHelper.RegisterRebindable(
			"Time 100%",
			"将游戏时间流速设为 100%",
			Key.Slash,
			GamepadBinding.None,
			false
		);

		// Teleport / Reparent hold keys
		TeleportBindType = ModHelper.RebindingHelper.RegisterRebindable(
            "传送(长按)",
            "长按时对选中的目标执行传送或设为父级",
			Key.T,
			GamepadBinding.None,
			false
		);
		ReparentBindType = ModHelper.RebindingHelper.RegisterRebindable(
            "重新设置父级(长按)",
			"长按时对选中的目标执行重新设置父级操作",
			Key.Y,
			GamepadBinding.None,
			false
		);

		CenterOnPlayerBindType = ModHelper.RebindingHelper.RegisterRebindable(
            "以玩家为中心",
            "将自由相机对准玩家位置.",
			Key.Digit0,
			GamepadBinding.None,
			false
		);

        // Center on planets
        CenterOnPlanetBindTypes[AstroObject.Name.Sun] = ModHelper.RebindingHelper.RegisterRebindable("以太阳为中心", "将自由相机对准太阳。", Key.Digit1, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.Comet] = ModHelper.RebindingHelper.RegisterRebindable("以闯入者为中心", "将自由相机对准闯入者。", Key.Digit2, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.CaveTwin] = ModHelper.RebindingHelper.RegisterRebindable("以余烬双星为中心", "将自由相机对准余烬双星。", Key.Digit3, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.TowerTwin] = ModHelper.RebindingHelper.RegisterRebindable("以灰烬双星为中心", "将自由相机对准灰烬双星。", Key.Digit4, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.TimberHearth] = ModHelper.RebindingHelper.RegisterRebindable("以木炉星为中心", "将自由相机对准木炉星。", Key.Digit5, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.BrittleHollow] = ModHelper.RebindingHelper.RegisterRebindable("以碎空星为中心", "将自由相机对准碎空星。", Key.Digit6, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.GiantsDeep] = ModHelper.RebindingHelper.RegisterRebindable("以深巨星为中心", "将自由相机对准深巨星。", Key.Digit7, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.DarkBramble] = ModHelper.RebindingHelper.RegisterRebindable("以黑棘星为中心", "将自由相机对准黑棘星。", Key.Digit8, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.RingWorld] = ModHelper.RebindingHelper.RegisterRebindable("以陌生人号为中心", "将自由相机对准陌生人号。", Key.Digit9, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.WhiteHole] = ModHelper.RebindingHelper.RegisterRebindable("以白洞为中心", "将自由相机对准白洞。", Key.F1, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.QuantumMoon] = ModHelper.RebindingHelper.RegisterRebindable("以量子卫星为中心", "将自由相机对准量子卫星。", Key.F2, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.ProbeCannon] = ModHelper.RebindingHelper.RegisterRebindable("以探测炮为中心", "将自由相机对准探测炮。", Key.F3, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.TimberMoon] = ModHelper.RebindingHelper.RegisterRebindable("以木炉卫星为中心", "将自由相机对准木炉卫星。", Key.F4, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.VolcanicMoon] = ModHelper.RebindingHelper.RegisterRebindable("以空心灯为中心", "将自由相机对准空心灯。", Key.F5, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.SunStation] = ModHelper.RebindingHelper.RegisterRebindable("以太阳站为中心", "将自由相机对准太阳站。", Key.F6, GamepadBinding.None, false);
        CenterOnPlanetBindTypes[AstroObject.Name.MapSatellite] = ModHelper.RebindingHelper.RegisterRebindable("以地图卫星为中心", "将自由相机对准地图卫星。", Key.F7, GamepadBinding.None, false);


        GlobalMessenger<OWCamera>.AddListener("SwitchActiveCamera", OnSwitchActiveCamera);

		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	public void OnDestroy()
	{
		GlobalMessenger<OWCamera>.RemoveListener("SwitchActiveCamera", OnSwitchActiveCamera);

		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	public override void Configure(IModConfig config)
	{
		_fov = config.GetSettingsValue<int>("FOV");
		_nearClipPlane = config.GetSettingsValue<int>("Near Clip Plane Distance");
		ShowPrompts = !config.GetSettingsValue<bool>("Hide Prompts");
		ShowTogglePrompt = !config.GetSettingsValue<bool>("Hide Toggle Prompt");
		ResetParent = config.GetSettingsValue<bool>("Reset Parent");

		// If the mod is currently active we can set these immediately
		if (_camera != null)
		{
			_camera.fieldOfView = _fov;
			_owCamera.fieldOfView = _fov;
			_camera.nearClipPlane = _nearClipPlane;
			_owCamera.nearClipPlane = _nearClipPlane;
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode _)
	{
		Write($"正在加载场景： {scene.name}");

		if (scene.name != "SolarSystem" && scene.name != "EyeOfTheUniverse") return;

		InFreeCam = false;

		(_owCamera, _camera) = _commonCameraAPI.CreateCustomCamera("FREECAM", (OWCamera cam) =>
		{
			cam.mainCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
			cam.mainCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("HeadsUpDisplay"));
			cam.mainCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("HelmetUVPass"));
		});
		_freeCam = _camera.gameObject;

		_freeCam.AddComponent<CustomLookAround>();
		_freeCam.AddComponent<CustomFlashlight>();
		_freeCam.AddComponent<FreeCamController>();
		_freeCam.AddComponent<PromptController>();

		_freeCam.SetActive(true);

		_hud = GameObject.Find("Player_Body/PlayerCamera/Helmet/HelmetRoot/HelmetMesh").gameObject;
	}

	private void OnSwitchActiveCamera(OWCamera camera)
	{
		if (InFreeCam && camera != _owCamera)
		{
			InFreeCam = false;
			Write($"Changing back to stored input mode {_storedMode}");
			if (_storedMode == InputMode.None)
			{
				_storedMode = InputMode.Character;
			}
			OWInput.ChangeInputMode(_storedMode);
			//ResetTimeScale();  我不想要这段代码
			ShowHUD();
			try
			{
				OnFreeCamExited?.Invoke();
			}
			catch (Exception e)
			{
				WriteError($"调用 OnFreeCamExited 事件时出错: {e}");
			}
		}
		else if (!InFreeCam && camera == _owCamera)
		{
			InFreeCam = true;
			Write($"Storing input mode {_storedMode}");
			_storedMode = OWInput.GetInputMode();
			OWInput.ChangeInputMode(InputMode.None);
			try
			{
				OnFreeCamEntered?.Invoke();
			}
			catch (Exception e)
			{
				WriteError($"Error invoking OnFreeCamEntered event: {e}");
			}
		}
	}

	public static void ResetTimeScale()
	{
		Time.timeScale = OWTime.IsPaused() ? 0 : OWTime.GetTimeScale();
	}

	public static void ToggleFreeCam()
	{
		if (InFreeCam)
		{
			Write("退出自由视角");
			_instance._commonCameraAPI.ExitCamera(_instance._owCamera);

			// Only re-enable the helmet HUD if we aren't already hiding the GUI
			if (!GUIMode.IsHiddenMode())
			{
				_instance._hud.SetActive(true);
			}
		}
		else
		{
			Write("进入自由视角");
			_instance._commonCameraAPI.EnterCamera(_instance._owCamera);
			_instance._hud.SetActive(false);
		}
	}

	public static void EnterFreeCam()
	{
		if (!InFreeCam)
		{
			ToggleFreeCam();
		}
	}

	public static void ExitFreeCam()
	{
		if (InFreeCam)
		{
			ToggleFreeCam();
		}
	}

	public static void ToggleHUD()
	{
		if (GUIMode.IsHiddenMode())
		{
			Write("显示HUD");
			GUIMode.SetRenderMode(GUIMode.RenderMode.FPS);

			// Turning the HUD back on while in free cam also shows the helmet HUD, which we don't want
			if (InFreeCam)
			{
				_instance._hud.SetActive(false);
			}
		}
		else
		{
			Write("隐藏HUD");
			GUIMode.SetRenderMode(GUIMode.RenderMode.Hidden);
		}
	}

	public static void ShowHUD()
	{
		if (GUIMode.IsHiddenMode())
		{
			ToggleHUD();
		}
	}

	public static void HideHUD()
	{
		if (!GUIMode.IsHiddenMode())
		{
			ToggleHUD();
		}
	}

	public static void Write(string msg) => _instance.ModHelper.Console.WriteLine($"[FreeCam] : {msg}", MessageType.Info);
	public static void WriteError(string msg) => _instance.ModHelper.Console.WriteLine($"[FreeCam] : {msg}", MessageType.Error);

	public override object GetApi()
	{
		return new FreeCamAPI();
	}


	public static bool TryGetSharedAxisID(InputAction first, InputAction second, bool gamepad, out AxisIdentifier axis)
	{
		axis = AxisIdentifier.NONE;
		UnityEngine.InputSystem.InputBinding bindingMask = (gamepad ? InputActionUtil.GamepadBindingMask : InputActionUtil.DesktopBindingMask);
		int bindingIndex = first.GetBindingIndex(bindingMask);
		int bindingIndex2 = second.GetBindingIndex(bindingMask);
		if (bindingIndex == -1 || bindingIndex2 == -1)
		{
			return false;
		}
		UnityEngine.InputSystem.InputBinding inputBinding = first.bindings[bindingIndex];
		UnityEngine.InputSystem.InputBinding inputBinding2 = second.bindings[bindingIndex2];
		if (inputBinding.effectivePath == inputBinding2.effectivePath)
		{
			string text;
			string path;
			inputBinding.ToDisplayString(out text, out path, UnityEngine.InputSystem.InputBinding.DisplayStringOptions.DontUseShortDisplayNames, null);
			return InputTransitionUtil.TryGetAxisIdentifier(path, out axis);
		}
		InputControl inputControl = InputSystem.FindControl(inputBinding.effectivePath) as AxisControl;
		InputControl inputControl2 = InputSystem.FindControl(inputBinding2.effectivePath) as AxisControl;
		if (inputControl == null || inputControl2 == null)
		{
			return gamepad && InputTransitionUtil.TryGetAxisIdentifier(inputBinding.effectivePath, inputBinding2.effectivePath, out axis);
		}
		return !(inputControl is DiscreteButtonControl) && !(inputControl2 is DiscreteButtonControl) && inputControl.parent != null && inputControl2.parent != null && inputControl.parent == inputControl2.parent && (InputTransitionUtil.TryGetAxisIdentifier(inputBinding.effectivePath, inputBinding2.effectivePath, out axis) || InputTransitionUtil.TryGetAxisIdentifier(inputControl.parent.name, out axis));
	}
}
