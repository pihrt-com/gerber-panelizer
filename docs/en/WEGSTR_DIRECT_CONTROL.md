# Direct Wegstr control

## Scope

The **Direct Wegstr control** tab prepares manufacturing operations directly from the current panel and Wegstr profile. It uses the same geometry and operation order as the export ZIP, but no G-code file is handed to another application: linear commands are compiled in memory to controller packets and sent through the serial port. The default mode is a simulator that sends nothing to hardware.

## What was verified from Wegstr 3.2.0

The analyzed artifact was the original `app/wegstr.jar` with SHA-256 `26b05fc0f74429c8e5183c15325093429f668d9648940735b21501956860d0cd`. Static class analysis and an independent run of the original encoder confirmed 9600 baud, 8 data bits, no parity, one stop bit and no flow control; a seven-byte packet containing three motion samples; header `FF` for a program and `3F` for manual mode; at most 14 steps per axis in one sample; and controller responses `1` ready, `2` spindle overload, `3` machine release/emergency state, and `4` probe contact.

The reference samples `(1,-2,3,on)`, `(-14,14,-8,off)`, `(0,0,14,on)` produce the identical packet `FF 61 92 1E 6E C0 A0` in the original JAR and Gerber Panelizer. A stationary spindle-off packet is `3F 00 00 00 00 00 00`. Automated tests also verify decoding of every sign, Z-axis bit, and spindle bit.

Each packet waits for one response; another packet is never sent while the first is unacknowledged. A two-second timeout, spindle overload, machine release, or an unexpected response stops the operation and prevents continuation without a new connection. STOP takes effect after the current packet exchange and is followed by a spindle-off packet.

## Original-application parity

| Wegstr 3.2.0 function | Gerber Panelizer direct control |
| --- | --- |
| 9600 8N1 controller connection | Yes; the user selects and explicitly confirms one port. Ports are not probed automatically at startup. |
| Offline mode | Yes; the safe default simulator uses the 59 ms acknowledgement timing observed in the original application. |
| Load and close a G-code file | Replaced by direct preparation of isolation, drilling, and outline operations from the current panel; no intermediate file is required. |
| Start, pause, continue, and STOP | Yes; pause and STOP occur at an acknowledged packet boundary. |
| Tool changes | Yes; the job pauses between tool blocks and requires Continue. |
| Progress and current tool | Yes. |
| Press-and-hold manual X/Y/Z | Yes; the original acceleration sequence `3, 6, 9, 10, 11, 12, 13, 14` steps is used before remaining at 14 steps per sample at 100%. |
| 1% stepping | Yes; one click sends three one-step samples, equal to 0.012 mm at 2500 steps/10 mm. |
| Fast positioning | Yes; **100% rapid** changes manual positioning only. It does not change machining feeds. |
| Work coordinates and X/Y/Z zeroing | Yes; coordinates can be edited or reset individually or together without machine motion. |
| Return to zero | Yes; Z first rises to the configured safe height, then X/Y return to zero, and finally Z returns to zero. |
| Spindle on/off | Yes; a stationary manual packet is used. |
| Probe auto-level | Yes. Response `4` consumes the following signed big-endian absolute-Z value. The probing plan contains only raster cells crossed by prepared spindle-on paths, the first touch is the reference, and compensation approaches the active cell correction one Z step per motion sample with configurable backlash compensation. |

## Automatic surface probing

Prepare all panel operations and connect the simulator before selecting **Measure surface**. The probing plan preserves the first encounter order of the raster cells crossed by isolation, drilling, and outline paths rather than measuring an unused rectangular area. At every point, Z rises to the configured probing safe height, X/Y moves to the stored path position, and Z descends until probe response `4` or the fixed Z -10 mm travel limit. After the final touch, the machine returns through safe Z to work zero and compensation is enabled automatically.

The first measured Z is the reference. Every other cell stores `reference - measured Z`; during machining the outgoing Z motion approaches the selected correction by at most one extra positive or negative step per sample. The configurable Z backlash value follows the original application's direction-change behavior. Preparing the panel again or editing/zeroing work coordinates clears the measurement because its coordinate basis is no longer valid. The deterministic simulator surface exercises the complete workflow without hardware.

## Manual positioning

For fast positioning above the board, connect the simulator first, verify every axis direction, and then select **100% rapid**. At 2–100%, motion continues while the button is held; release takes effect after the currently transmitted packet is acknowledged. A value of 1% intentionally performs one step packet rather than continuous motion. This setting applies only to manual control and return to zero; cutting and drilling feeds remain those of the Wegstr profile and prepared program.

## Acceptance checklist

1. On the Panel tab, import the data, select the work area, place complete boards, and inspect layers, orientation, and profiles.
2. In Settings, confirm X/Y/Z calibration within 2400–2600 steps per 10 mm, safe Z, depths, tools, manufacturing feeds, probing raster (0.5–70 mm), probing safe Z (0.1–40 mm), and Z backlash (0–30 steps).
3. Open **Direct Wegstr control**, keep the simulator enabled, choose **Prepare panel data**, and review every operation, packet count, and tool change.
4. In the simulator, verify stepping, continuous movement, STOP, zeroing, spindle control, return to zero, the full surface measurement, its point count and correction range, and a compensated program run.
5. Only then disable the simulator, select the correct COM port, and confirm the connection. The application sends a stationary identification packet and must report ready.
6. With the spindle disconnected or the tool removed and Z safely above the workpiece, verify X/Y/Z signs, a measured 10 mm distance, the default-calibration 0.012 mm step, STOP, and the physical emergency stop.
7. Verify probe wiring and polarity independently, use a coarse raster over sacrificial material, keep access to emergency stop, and confirm that every touch reports a plausible absolute Z before allowing compensation.
8. Complete a full raised-tool dry run above sacrificial material. Begin machining only after verifying the work origin, clamping, tool, safe height, probe removal where required, and spindle clearance.

## Verification boundary

Packet encoding, probe-response parsing, path-scoped raster planning, first-touch referencing, gradual bounded compensation, manual acceleration, stepping, exact motion totals, the ±14-step limit, compilation of generated linear G-code, simulated acknowledgements, state transitions, and the graphical controls were verified automatically or interactively. The test environment was not connected to a physical Wegstr machine, so this document does not claim production acceptance of a particular controller, cable, axis direction, probe, calibration, or spindle.
