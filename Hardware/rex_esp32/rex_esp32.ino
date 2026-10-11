// Rex: one ESP32 (T-Display) for the EMG armband AND the haptic vest.
//
// Wiring (see the wiring diagram):
//   MyoWare 2.0  VIN -> 3V   GND -> GND   ENV -> GPIO 32   (RECT and RAW unused)
//   Relay board  IN1 -> GPIO 25 (left)   IN2 -> GPIO 26 (right)   IN3 -> GPIO 27 (back)
//                GND -> GND   VCC -> 5V   (motor battery stays separate)
//
// Network (UDP, both directions on the same Wi-Fi):
//   ESP32 -> game  port 5005  text "0.000" .. "1.000"  raw muscle level, about 50 per second
//                             (the game does the relax / flex calibration)
//   game  -> ESP32 port 5006  text "L,200" / "R,200" / "F,200"  side + strength 0..255
//                             L = left motor, R = right motor, F (hit from the front) = all three
//
// Arduino IDE: board "ESP32 Dev Module" (or the LilyGO T-Display entry), no extra libraries needed.
// Open Serial Monitor at 115200 to see the IP address and every hit.

#include <WiFi.h>
#include <WiFiUdp.h>

// ---------------------------------------------------------------- settings
const char* WIFI_NAME     = "HALAR_HOTSPOT_NAME";   // <- Halar's hotspot name (2.4 GHz)
const char* WIFI_PASSWORD = "HOTSPOT_PASSWORD";     // <- its password

const int  PIN_EMG   = 32;
const int  PIN_LEFT  = 25;
const int  PIN_RIGHT = 26;
const int  PIN_BACK  = 27;

// Most cheap relay boards switch ON when the input pin goes LOW. If the motors buzz
// at power-up and stop when hit, change this to false.
const bool RELAY_ACTIVE_LOW = true;

// Relays are on/off only, so a stronger hit = a longer buzz.
const int  BUZZ_MIN_MS = 120;   // weakest hit
const int  BUZZ_MAX_MS = 400;   // strongest hit

const int  PORT_EMG  = 5005;
const int  PORT_VEST = 5006;
const int  EMG_SEND_EVERY_MS = 20;   // 50 Hz
// ----------------------------------------------------------------

WiFiUDP udpVest;   // listens for hits
WiFiUDP udpEmg;    // sends the muscle level
IPAddress gameIp;  // learned from the first hit packet; until then the EMG value is broadcast
bool haveGameIp = false;

const int motorPins[3] = { PIN_LEFT, PIN_RIGHT, PIN_BACK };
unsigned long motorOffAt[3] = { 0, 0, 0 };
bool motorOn[3] = { false, false, false };

float emgSmooth = 0;
unsigned long nextEmgSend = 0;
unsigned long lastWifiCheck = 0;

void setMotor(int i, bool on)
{
  motorOn[i] = on;
  digitalWrite(motorPins[i], (on ^ RELAY_ACTIVE_LOW) ? HIGH : LOW);
}

void buzz(int i, int strength)
{
  strength = constrain(strength, 0, 255);
  if (strength == 0) return;
  unsigned long ms = BUZZ_MIN_MS + (unsigned long)(BUZZ_MAX_MS - BUZZ_MIN_MS) * strength / 255;
  setMotor(i, true);
  unsigned long off = millis() + ms;
  if (off > motorOffAt[i]) motorOffAt[i] = off;   // a new hit can only make a buzz longer
}

void connectWifi()
{
  Serial.printf("Connecting to %s", WIFI_NAME);
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);   // lower, steadier latency
  WiFi.begin(WIFI_NAME, WIFI_PASSWORD);
  unsigned long giveUp = millis() + 15000;
  while (WiFi.status() != WL_CONNECTED && millis() < giveUp) { delay(250); Serial.print("."); }
  Serial.println();
  if (WiFi.status() == WL_CONNECTED)
  {
    Serial.print("Connected. ESP32 IP: ");
    Serial.println(WiFi.localIP());
    udpVest.begin(PORT_VEST);
    // little "I'm alive" buzz on all three motors
    for (int i = 0; i < 3; i++) buzz(i, 40);
  }
  else Serial.println("Wi-Fi not found, will retry. Check WIFI_NAME / WIFI_PASSWORD and that the hotspot is 2.4 GHz.");
}

void setup()
{
  Serial.begin(115200);
  for (int i = 0; i < 3; i++) { pinMode(motorPins[i], OUTPUT); setMotor(i, false); }
  analogReadResolution(12);
  analogSetPinAttenuation(PIN_EMG, ADC_11db);   // full 0..3.3 V range
  connectWifi();
}

void handleHits()
{
  int size = udpVest.parsePacket();
  while (size > 0)
  {
    char buf[32];
    int n = udpVest.read(buf, sizeof(buf) - 1);
    buf[n > 0 ? n : 0] = 0;
    if (!haveGameIp) { gameIp = udpVest.remoteIP(); haveGameIp = true; Serial.print("Game found at "); Serial.println(gameIp); }

    char side = buf[0];
    int strength = 200;
    char* comma = strchr(buf, ',');
    if (comma) strength = atoi(comma + 1);

    if      (side == 'L') buzz(0, strength);
    else if (side == 'R') buzz(1, strength);
    else if (side == 'F') { buzz(0, strength); buzz(1, strength); buzz(2, strength); }   // bite from the front: whole vest
    else if (side == 'B') buzz(2, strength);
    Serial.printf("hit %s\n", buf);

    size = udpVest.parsePacket();
  }
}

void updateMotors()
{
  unsigned long now = millis();
  for (int i = 0; i < 3; i++)
    if (motorOn[i] && (long)(now - motorOffAt[i]) >= 0) setMotor(i, false);
}

void sendEmg()
{
  unsigned long now = millis();
  if ((long)(now - nextEmgSend) < 0) return;
  nextEmgSend = now + EMG_SEND_EVERY_MS;

  // average a few samples to calm the ADC noise
  long sum = 0;
  for (int i = 0; i < 8; i++) sum += analogRead(PIN_EMG);
  float v = (sum / 8.0f) / 4095.0f;
  emgSmooth += (v - emgSmooth) * 0.35f;

  char msg[12];
  snprintf(msg, sizeof(msg), "%.3f", emgSmooth);
  udpEmg.beginPacket(haveGameIp ? gameIp : IPAddress(255, 255, 255, 255), PORT_EMG);
  udpEmg.print(msg);
  udpEmg.endPacket();
}

void loop()
{
  updateMotors();

  if (WiFi.status() != WL_CONNECTED)
  {
    for (int i = 0; i < 3; i++) setMotor(i, false);   // never leave a motor stuck on
    if (millis() - lastWifiCheck > 5000) { lastWifiCheck = millis(); WiFi.disconnect(); connectWifi(); }
    return;
  }

  handleHits();
  sendEmg();
}
