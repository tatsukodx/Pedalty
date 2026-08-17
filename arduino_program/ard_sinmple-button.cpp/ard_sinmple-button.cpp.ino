#include "config.h"

// 全ピン内蔵プルアップ（ピン↔GND接続）。離している=HIGH、押している=LOW
#define IS_PRESSED(v) ((v) == LOW)

bool rightState = false;
bool leftState = false;
bool rightRaw = false;
bool leftRaw = false;
unsigned long rightChangedAt = 0;
unsigned long leftChangedAt = 0;
uint32_t btnNextMs = 0;

volatile bool magnetTriggered = false;
volatile unsigned long lastTriggerTime = 0;
volatile unsigned long triggerInterval = 0;

unsigned long ledOffTime = 0;
bool stoppedSent = true;

uint32_t potNextMs = 0;

// 値が BTN_DEBOUNCE_MS の間変化しなかったときだけ状態を確定させる
bool debounce(bool raw, bool &lastRaw, bool &state, unsigned long &changedAt)
{
  unsigned long now = millis();
  if (raw != lastRaw)
  {
    lastRaw = raw;
    changedAt = now;
  }
  else if (raw != state && now - changedAt >= BTN_DEBOUNCE_MS)
  {
    state = raw;
  }
  return state;
}

void calcVelocity()
{
  unsigned long now = millis();
  if (lastTriggerTime != 0 && now - lastTriggerTime < MAGNET_CHATTER_MS)
    return; // チャタリング対策

  // 初回検出時は1秒間隔として扱い、最初の1回から走り始めるようにする
  triggerInterval = lastTriggerTime == 0 ? 1000 : now - lastTriggerTime;
  lastTriggerTime = now;
  magnetTriggered = true;
}

// ランダムノイズを均すため複数回サンプリングして平均する
int readPot()
{
  uint16_t sum = 0;
  for (uint8_t i = 0; i < POT_SAMPLES; i++) sum += analogRead(PIN_POT);
  return sum / POT_SAMPLES;
}

void updatePot(uint32_t now)
{
  // 単純な大小比較だと millis() のロールオーバー時に送信が止まる
  if ((int32_t)(now - potNextMs) < 0) return;
  potNextMs = now + POT_PERIOD_MS;

  Serial.print(F("POT,"));
  Serial.println(readPot());
}

void updateButtons(uint32_t now)
{
  // 判定を安定させるため、読み取り自体は毎ループ行う
  bool r = debounce(IS_PRESSED(digitalRead(PIN_BTN_RIGHT)), rightRaw, rightState, rightChangedAt);
  bool l = debounce(IS_PRESSED(digitalRead(PIN_BTN_LEFT)), leftRaw, leftState, leftChangedAt);

  if ((int32_t)(now - btnNextMs) < 0) return;
  btnNextMs = now + BTN_PERIOD_MS;

  // 押=1 / 離=0 で送信
  Serial.print(r ? 1 : 0);
  Serial.print(",");
  Serial.println(l ? 1 : 0);
}

void updateMagnet(uint32_t now)
{
  // 割り込みで更新される値を安全にコピーする
  noInterrupts();
  bool triggered = magnetTriggered;
  unsigned long interval = triggerInterval;
  unsigned long lastTrigger = lastTriggerTime;
  magnetTriggered = false;
  interrupts();

  if (triggered)
  {
    Serial.print(F("MAGNET,"));
    Serial.println(interval);
    stoppedSent = false;

    digitalWrite(LED_BUILTIN, HIGH);
    ledOffTime = now + 80;
  }
  else if (!stoppedSent && lastTrigger != 0 && now - lastTrigger > MAGNET_STOP_MS)
  {
    Serial.println(F("MAGNET,0"));
    stoppedSent = true;
  }

  if (ledOffTime != 0 && (int32_t)(now - ledOffTime) >= 0)
  {
    digitalWrite(LED_BUILTIN, LOW);
    ledOffTime = 0;
  }
}

void setup()
{
  Serial.begin(115200);

  pinMode(PIN_BTN_RIGHT, INPUT_PULLUP);
  pinMode(PIN_BTN_LEFT, INPUT_PULLUP);
  pinMode(PIN_MAGNET, INPUT_PULLUP);
  pinMode(LED_BUILTIN, OUTPUT);

  attachInterrupt(digitalPinToInterrupt(PIN_MAGNET), calcVelocity, FALLING);

  analogRead(PIN_POT); // マルチプレクサ切替直後は不正確なので1回目は捨てる
}

void loop()
{
  uint32_t now = millis();

  updatePot(now);
  updateButtons(now);
  updateMagnet(now);
}
