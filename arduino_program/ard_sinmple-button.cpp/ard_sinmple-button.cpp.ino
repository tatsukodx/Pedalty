// ===== ピン定義 =====
#define RIGHT 3  // 右ボタン（D3）
#define LEFT 4   // 左ボタン（D4）
#define MAGNET 2 // マグネットセンサ（D2）・割り込み使用

// ===== マグネットセンサ用 =====
volatile bool magnetTriggered = false;
volatile unsigned long lastTriggerTime = 0;
unsigned long interval = 0;

void calcVelocity() {
  unsigned long now = millis();
  if (now - lastTriggerTime < 10)
    return; // チャタリング対策
  interval = now - lastTriggerTime;
  lastTriggerTime = now;
  magnetTriggered = true;
}

// ===== セットアップ =====
void setup() {
  Serial.begin(115200);

  pinMode(RIGHT, INPUT);
  pinMode(LEFT, INPUT);
  pinMode(MAGNET, INPUT_PULLUP);

  attachInterrupt(digitalPinToInterrupt(MAGNET), calcVelocity, FALLING);
}

// ===== メインループ =====
void loop() {
  // --- ボタン送信 ---
  int r = digitalRead(RIGHT);
  int l = digitalRead(LEFT);
  Serial.print(r);
  Serial.print(",");
  Serial.println(l);

  // --- マグネット送信 ---
  // 2秒間反応なし → 停止とみなす
  if (millis() - lastTriggerTime > 2000) {
    interval = 0;
  }

  if (magnetTriggered) {
    magnetTriggered = false;
    Serial.print("MAGNET,");
    Serial.println(interval);
  } else if (interval == 0) {
    Serial.println("MAGNET,0");
  }

  delay(50);
}