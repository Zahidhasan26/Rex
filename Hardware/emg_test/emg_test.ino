// MyoWare 2.0 bench test: no Wi-Fi, no game. Shows the muscle signal on GPIO 32.
// Upload, then open Tools > Serial Plotter at 115200.
// Relaxed arm: a low, flat line. Squeezing your fist: the line jumps clearly.
// Flat no matter what: check VIN -> 3V, GND -> GND, ENV -> 32, the MyoWare power LED,
// and electrode placement (forearm flexors, reference electrode on the elbow bone).

const int PIN_EMG = 32;
float smooth = 0;

void setup()
{
  Serial.begin(115200);
  analogReadResolution(12);
  analogSetPinAttenuation(PIN_EMG, ADC_11db);
}

void loop()
{
  long sum = 0;
  for (int i = 0; i < 8; i++) sum += analogRead(PIN_EMG);
  int raw = sum / 8;                 // 0..4095
  smooth += (raw - smooth) * 0.35f;
  Serial.print("raw:");     Serial.print(raw);
  Serial.print(" smooth:"); Serial.println((int)smooth);
  delay(20);
}
