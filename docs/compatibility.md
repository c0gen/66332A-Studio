# 66332A Compatibility mode over RS-232

The Service workspace identifies the programming language before selecting commands. A language change requires an identified, qualified connection and a physically isolated load. The instrument stores the choice and restarts, so the app requests off immediately. Compatibility mode has no documented output-enable query. Its output status stays unknown after an off request, even when `ERR?` reports no error. Returning to SCPI verifies the instrument identity and output off before standard monitoring resumes. If `ID?` in Compatibility reports an emulated model name instead of an exact 66332A identity, select SCPI from the instrument’s front panel Address menu and reconnect. [Programming Guide 5962-8198, pp. 69, 83–87](https://www.keysight.com/mt/en/assets/9018-01225/programming-guides/9018-01225.pdf#page=84)

| Legacy command | App workflow |
| --- | --- |
| `SYST:LANG`, `ID?`, `ROM?` | Service language selection and legacy connection identification. |
| `VOUT?`, `IOUT?`, `STS?` | Read legacy measurements and live status. |
| `ASTS?`, `FAULT?`, `ERR?` | Acknowledge latched legacy status/faults, or read and clear the error alone. |
| `UNMASK`, `SRQ`, `PON` | Apply the legacy fault mask, service-request enable, or power-on request. The app reports acknowledgment through `ERR?`; these settings have no documented query readback. `PON` is limited to one command per mains power cycle. |
| `RLYPON` | Set the legacy startup relay after confirming Option 760 in SCPI during the same connection and isolating the fixture. |
| `TEST?`, `CLR` | Run isolated self-test or clear and request off. Clear can restore a power-on state before the following off request. Confirm the physical output at the instrument. |
| `OUT`, `DC`, `RELAY`, `POL` | Use SCPI Monitor and Instrument Setup for verified converter and relay control. The global Output Off action can send `OUT 0` in Compatibility and reports shutdown as unverified. |
| `VSET`, `ISET`, `OVSET`, `OCP`, `DLY`, `RST` | Use SCPI Monitor for verified setpoints, protection settings and fault clearing. |
| `DSP` | Use SCPI Instrument Setup for verified front panel display control. |
| `SENS:CURR:RANG`, `SENS:SWE:POIN`, `SENS:SWE:TINT` | Use SCPI Capture for verified measurement settings. |

PON and SRQ configure internal behavior. Delivery of IEEE-488 service-request interrupts, native serial poll and other bus operations requires a GPIB connection. RS-232 reports legacy condition, fault, and error data through the documented queries. [Programming Guide, pp. 34–35 and 83–87](https://www.keysight.com/mt/en/assets/9018-01225/programming-guides/9018-01225.pdf#page=84)
