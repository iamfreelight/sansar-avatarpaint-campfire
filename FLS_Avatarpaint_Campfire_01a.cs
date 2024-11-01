/* 
 * AvatarPaint Script - Campfire version
 * by Freelight - 11/1/2024 
 *
 * Makes avatar's turn the 'normal tint' when near the scene's campfire's 'WarmAreaTrigger'.. but when they leave it they turn 'ColdColor' (cyan/blue works good).. when 
 * they enter the 'BurnAreaTrigger' it makes the avatar look burnt and plays a sound -- make this trigger very small and put right in the center of the campfire -
 * the two triggers can overlap and work just fine
 *
 * https://github.com/iamfreelight/sansar-avatarpaint-campfire
 *
 */

using Sansar;
using Sansar.Script;
using Sansar.Simulation;
using System;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

public class FLS_Avatarpaint_Campfire_01a : SceneObjectScript
{
	[DefaultValue(false)]
	public bool DebugMode = false;
	
	[DefaultValue(true)]
	public bool ColdOnJoin = true;

	[DefaultValue(3.0f)]
	public float EffectEmissLevelCold = 3.0f;
	
	[DefaultValue(0,1,1,0.5)]
	public Sansar.Color ColdColor;
	
	[DefaultValue(1.5f)]
	public float EffectSpeedCold = 1.5f;

	public RigidBodyComponent WarmAreaTrigger = null;	
	[DefaultValue(1.5f)]
	public float WarmingCleaningSpeed = 1.5f;	
	public RigidBodyComponent BurnAreaTrigger = null;
	
	public float EffectEmissLevelBurnt = 0.0f;
	
	[DefaultValue(0,0,0,1f)]
	public Sansar.Color BurntColor = new Sansar.Color(0f,0f,0f,1f);
	[DefaultValue(1.5f)]
	public float EffectSpeedBurn = 1.5f;
	public SoundResource OnBurnSound;

	public class AvatarMatsData {
		public Sansar.Color[] origMatColors = new Sansar.Color[2048];
		public float[] origMatEmiss = new float[2048];
	}
	
	private List<Tuple<ObjectId, AvatarMatsData>> AMD = new List<Tuple<ObjectId, AvatarMatsData>>();

	public interface ISimpleData
	{
		AgentInfo AgentInfo { get; }
		ObjectId ObjectId { get; }
		ObjectId SourceObjectId { get; }
		Reflective ExtraData { get; }
	}

	public class SimpleData : Reflective, ISimpleData
	{
		public SimpleData(ScriptBase script) { ExtraData = script; }
		public AgentInfo AgentInfo { get; set; }
		public ObjectId ObjectId { get; set; }
		public ObjectId SourceObjectId { get; set; }
		public Reflective ExtraData { get; }
	}

	private static readonly Random rnd = new Random();
	private float GetRandomFloat()
	{
		return (float)rnd.NextDouble();
	}

	InterpolationMode InterpolationModeParse(string s)
	{
		s = s.ToLower();
		if (s == "easein") return InterpolationMode.EaseIn;
		if (s == "easeout") return InterpolationMode.EaseOut;
		if (s == "linear") return InterpolationMode.Linear;
		if (s == "smoothstep") return InterpolationMode.Smoothstep;
		if (s == "step") return InterpolationMode.Step;
		if (DebugMode == true) Log.Write(LogLevel.Warning, $"Unknown InterpolationMode '{s}'!  Using Linear...");
		return InterpolationMode.Linear;
	}

	private void OnTriggerWarm(CollisionData data)
	{
		try {
			AgentPrivate agent = ScenePrivate.FindAgent(data.HitComponentId.ObjectId);
			ObjectId agentObjId;

			if (data.Phase == CollisionEventPhase.TriggerExit)
			{
				SimpleData sd = new SimpleData(this);
				sd.ObjectId = data.HitComponentId.ObjectId;
				sd.AgentInfo = ScenePrivate.FindAgent(sd.ObjectId)?.AgentInfo;
				sd.SourceObjectId = ObjectPrivate.ObjectId;
				agentObjId = sd.AgentInfo.ObjectId;
				
				if (agentObjId != null) {
					MeshComponent mc = null;
					ScenePrivate.FindObject(agentObjId).TryGetComponent(0, out mc);
					bool isVisible = mc.GetIsVisible();
					if (isVisible == true) {
						DoColorize(mc, EffectSpeedCold, EffectEmissLevelCold, ColdColor.R, ColdColor.G, ColdColor.B, ColdColor.A);
						if (DebugMode == true) Log.Write("Cooling avatar materials for '"+sd.AgentInfo.Handle.Trim()+"' @ TriggerWarmExit");
					}
				}
	
			}
			else if (data.Phase == CollisionEventPhase.TriggerEnter)
			{
				SimpleData sd = new SimpleData(this);
				sd.ObjectId = data.HitComponentId.ObjectId;
				sd.AgentInfo = ScenePrivate.FindAgent(sd.ObjectId)?.AgentInfo;
				sd.SourceObjectId = ObjectPrivate.ObjectId;
				agentObjId = sd.AgentInfo.ObjectId;

				AvatarMatsData avatarMatsData = GetAvatarMatsData(sd.AgentInfo.ObjectId);

				if (avatarMatsData != null) {
					MeshComponent mc = null;
					ScenePrivate.FindObject(agentObjId).TryGetComponent(0, out mc);					
					
					if (mc != null) {
						mc.SetIsVisible(true);						
						List<RenderMaterial> materials2 = mc.GetRenderMaterials().ToList();
						for (int j = 0; j < materials2.Count; j++)
						{
							MaterialProperties p = materials2[j].GetProperties();
							p.Tint = avatarMatsData.origMatColors[j];
							p.EmissiveIntensity = avatarMatsData.origMatEmiss[j];
							materials2[j].SetProperties(p, WarmingCleaningSpeed, InterpolationModeParse("linear"));
							if (DebugMode == true) Log.Write("Warming avatar materials for '"+sd.AgentInfo.Handle.Trim()+"' @ TriggerWarmEnter");
						}
					}
				}
			}
		} catch {}
	}

	private void OnTriggerBurn(CollisionData data)
	{
		try {
			AgentPrivate agent = ScenePrivate.FindAgent(data.HitComponentId.ObjectId);
			ObjectId agentObjId;

			if (data.Phase == CollisionEventPhase.TriggerEnter)
			{
				SimpleData sd = new SimpleData(this);
				sd.ObjectId = data.HitComponentId.ObjectId;
				sd.AgentInfo = ScenePrivate.FindAgent(sd.ObjectId)?.AgentInfo;
				sd.SourceObjectId = ObjectPrivate.ObjectId;
				agentObjId = sd.AgentInfo.ObjectId;
				
				if (agentObjId != null) {
					MeshComponent mc = null;
					ScenePrivate.FindObject(agentObjId).TryGetComponent(0, out mc);
					bool isVisible = mc.GetIsVisible();
					if (isVisible == true) {
						if ((audio != null) && (OnBurnSound != null)) PlaySound(OnBurnSound);
						if (DebugMode == true) Log.Write("Burning avatar materials for '"+sd.AgentInfo.Handle.Trim()+"'");
						DoColorize(mc, EffectSpeedBurn, EffectEmissLevelBurnt, BurntColor.R, BurntColor.G, BurntColor.B, BurntColor.A);
					}
				}
			}
		} catch {}
	}

	private void DoColorize(MeshComponent mc, float effectSpeed, float effectEmissLevel, float r, float g, float b, float a) {
		List<RenderMaterial> materials = mc.GetRenderMaterials().ToList();
		for (int j = 0; j < materials.Count; j++)
		{
			MaterialProperties p = materials[j].GetProperties();
			Sansar.Color newcolor = new Sansar.Color(r,g,b,a);
			p.Tint = newcolor;
			p.EmissiveIntensity = effectEmissLevel;
			materials[j].SetProperties(p, effectSpeed, InterpolationModeParse("linear"));
		}
	}

	public override void Init() {
		WarmAreaTrigger.Subscribe(CollisionEventType.Trigger, OnTriggerWarm);
		BurnAreaTrigger.Subscribe(CollisionEventType.Trigger, OnTriggerBurn);
		
        	ScenePrivate.User.Subscribe(User.AddUser, OnUserJoin);
		
		for (uint aui = 0; aui < ObjectPrivate.GetComponentCount(ComponentType.AudioComponent); ++aui)
		{
			if (ObjectPrivate.TryGetComponent(aui, out audio))
			{
				if (DebugMode == true) Log.Write("Located AudioComponent in Init()!");
				break;
			}
		}		
	}
	
	public void AddEntry(ObjectId objectId, AvatarMatsData avatarMatsData)
	{
		// Check if the ObjectId already exists in the list
		bool exists = AMD.Any(tuple => tuple.Item1.Equals(objectId));

		if (!exists)
		{
			// Only add if the ObjectId doesn't already exist
			AMD.Add(Tuple.Create(objectId, avatarMatsData));
			if (DebugMode == true) Log.Write("Entry added: " + objectId.ToString());
		}
		else
		{
			if (DebugMode == true) Log.Write("Entry with this ObjectId already exists.");
		}
	}
	
	public AvatarMatsData GetAvatarMatsData(ObjectId objectId)
	{
		// Search for the first tuple with a matching ObjectId
		var foundTuple = AMD.Find(tuple => tuple.Item1.Equals(objectId));

		if (foundTuple != null)
		{
			// Return the AvatarMatsData if a match is found
			return foundTuple.Item2;
		}

		// Return null or handle the case if no match is found
		if (DebugMode == true) Log.Write("No AvatarMatsData found for the specified ObjectId.");
		return null;
	}
	
	private void OnUserJoin(UserData data)
    	{
		try {
			//save avatars original materials via OnJoin, so they can be restored when going inside the WarmAreaTrigger
			AgentPrivate agent = ScenePrivate.FindAgent(data.User);
			ObjectId agentObjId = agent.AgentInfo.ObjectId;
			MeshComponent mc = null;
			ScenePrivate.FindObject(agentObjId).TryGetComponent(0, out mc);

			if (mc != null) {
				AvatarMatsData tmpDataToAdd = new AvatarMatsData();
				List<RenderMaterial> materials = mc.GetRenderMaterials().ToList();
				for (int j = 0; j < materials.Count; j++)
				{
					MaterialProperties p = materials[j].GetProperties();
					tmpDataToAdd.origMatColors[j] = p.Tint;
					tmpDataToAdd.origMatEmiss[j] = p.EmissiveIntensity;
				}

				AddEntry(agent.AgentInfo.ObjectId, tmpDataToAdd);
			}
			
			//after saving avatars original materials, make them cold by default when joining, if ColdOnJoin is true
			if (ColdOnJoin == true) {
				if (agentObjId != null) {
					MeshComponent mc2 = null;
					ScenePrivate.FindObject(agentObjId).TryGetComponent(0, out mc2);
					bool isVisible = mc2.GetIsVisible();
					if (isVisible == true) {
						DoColorize(mc2, EffectSpeedCold, EffectEmissLevelCold, ColdColor.R, ColdColor.G, ColdColor.B, ColdColor.A);
						if (DebugMode == true) Log.Write("Cooling avatar materials for '"+agent.AgentInfo.Handle.Trim()+"' @ TriggerWarmExit");
					}
				}
			}
		} catch {
			if (DebugMode == true) Log.Write("OnUserJoin() - Exception");
		}
	}
	
	// // // // // start - sound stuff // // // // //
	private float PitchVariance = 0f;
	private float LoudnessVariance = 0f;
	public Sansar.Vector AudioOffset = new Sansar.Vector();	
	[Tooltip(@"The minimum loudness the winning sounds will be played at.")]
	[DefaultValue(50.0f)]
	[Range(0.0f, 100.0f)]
	public readonly float Loudness;
	private float AdjustFadeTime = 0.5f;
	private AudioComponent audio;
	private PlayHandle currentPlayHandle = null;
	private ICoroutine fadeCoroutine = null;
	private float fadeTime = 0.0f;
	private float previousLoudness = 0.0f;
	private float previousPitchShift = 0.0f;
	private float targetLoudness = 0.0f;
	private float targetPitchShift = 0.0f;	
	
	private void StopSound(bool fadeout)
	{
		if (currentPlayHandle != null)
		{
			currentPlayHandle.Stop(fadeout);
			currentPlayHandle = null;
		}
	}

	private float RandomNegOneToOne()
	{
		return (float)(rnd.NextDouble() * 2.0 - 1.0);
	}

	private float LoudnessPercentToDb(float loudnessPercent)
	{
		loudnessPercent = Math.Min(Math.Max(loudnessPercent, 0.0f), 100.0f);
		return 60.0f * (loudnessPercent / 100.0f) - 48.0f;
	}

	private float LoudnessDbToPercent(float loudnessDb)
	{
		float percent = (loudnessDb + 48.0f) * 100.0f / 60.0f;
		return Math.Min(Math.Max(percent, 0.0f), 100.0f);
	}

	private void PlaySound(SoundResource sound)
	{
		PlaySettings playSettings = PlaySettings.PlayOnce;
		playSettings.DontSync = false;
		currentPlayHandle = ScenePrivate.PlaySoundAtPosition(sound, ObjectPrivate.Position + AudioOffset, playSettings);
	}
	
	private void AdjustSound(float loudness, float pitchOffset)
	{
		if ((currentPlayHandle != null) && currentPlayHandle.IsPlaying())
		{
			targetLoudness = loudness + LoudnessVariance * RandomNegOneToOne();
			targetPitchShift = pitchOffset + PitchVariance * RandomNegOneToOne();

			if (AdjustFadeTime > 0.0f)
			{
				previousLoudness = LoudnessDbToPercent(currentPlayHandle.GetLoudness());
				previousPitchShift = currentPlayHandle.GetPitchShift();

				fadeTime = AdjustFadeTime;
			}
			else
			{
				fadeTime = 0.0f;

				float targetLoudnessDb = LoudnessPercentToDb(targetLoudness);
				currentPlayHandle.SetLoudness(targetLoudnessDb);
				currentPlayHandle.SetPitchShift(targetPitchShift);
			}
		}
	}

	private void StartFadeCoroutine()
	{
		if ((fadeCoroutine == null) && (AdjustFadeTime > 0.0f))
		{
			fadeTime = 0.0f;

			previousLoudness = 0.0f;
			previousPitchShift = 0.0f;

			fadeCoroutine = StartCoroutine(FadeSoundAdjustments);
		}
	}

	private void StopFadeCoroutine()
	{
		if (fadeCoroutine != null)
		{
			fadeCoroutine.Abort();
			fadeCoroutine = null;
		}
	}

	private void FadeSoundAdjustments()
	{
		const float deltaTime = 0.1f;
		TimeSpan ts = TimeSpan.FromSeconds(deltaTime);

		while (true)
		{
			Wait(ts);

			if ((fadeTime > 0.0f) && (currentPlayHandle != null) && currentPlayHandle.IsPlaying())
			{
				fadeTime = Math.Max(fadeTime - deltaTime, 0.0f);

				float t = fadeTime / AdjustFadeTime;

				float loudness = previousLoudness * t + targetLoudness * (1.0f - t);
				float pitchShift = previousPitchShift * t + targetPitchShift * (1.0f - t);

				float loudnessDb = LoudnessPercentToDb(loudness);
				currentPlayHandle.SetLoudness(loudnessDb);
				currentPlayHandle.SetPitchShift(pitchShift);
			}
		}
	}
	// // // // // end - sound stuff // // // // //
}
