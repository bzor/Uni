using System;                      // For Action<>
using UnityEngine;
using UnityEngine.InputSystem;
using Minis;

public class Pc4MinisListener : MonoBehaviour
{
	[Tooltip("PC4 default = MIDI channel 1 (Minis channel index = 0)")]
	[Range(1, 16)]
	public int pc4MidiChannel = 1;

	/// <summary>
	/// Fired whenever a CC (knob) value changes on this listener's channel.
	/// int  = CC number (e.g. 1..24 for PC4 pots)
	/// float = value (0..1)
	/// </summary>
	public event Action<int, float> KnobChanged;

	void OnEnable()
	{
		InputSystem.onDeviceChange += OnDeviceChange;
	}

	void OnDisable()
	{
		InputSystem.onDeviceChange -= OnDeviceChange;
	}

	void OnDeviceChange(InputDevice device, InputDeviceChange change)
	{
		if (change != InputDeviceChange.Added)
			return;

		var midiDevice = device as MidiDevice;
		if (midiDevice == null)
			return;

		// Minis uses 0-based MIDI channel indices.
		int minisChannelIndex = pc4MidiChannel - 1;
		if (midiDevice.channel != minisChannelIndex)
			return;

		Debug.Log($"[Pc4MinisListener] MIDI device added on channel {midiDevice.channel}");

		// Subscribe to CC (Control Change) messages on this device/channel.
		midiDevice.onWillControlChange += (cc, value) =>
		{
			int ccNumber = cc.controlNumber; // e.g. 1..24 on PC4
			float v = (float)value;          // normalized 0..1

			// Optional debug:
			// Debug.Log($"[Pc4MinisListener] CC {ccNumber} = {v:0.000}");

			// Raise C# event so other scripts can listen cleanly.
			KnobChanged?.Invoke(ccNumber, v);
		};
	}
}