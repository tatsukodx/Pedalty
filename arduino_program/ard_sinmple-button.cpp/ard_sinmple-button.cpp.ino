#define RIGHT 3
#define LEFT 4

void setup() {
  Serial.begin(115200);
  pinMode(RIGHT, INPUT);
  pinMode(LEFT, INPUT);
}

void loop() {
  int r = digitalRead(RIGHT);
  int l = digitalRead(LEFT);
  Serial.print(r);
  Serial.print(",");
  Serial.println(l);
  delay(50); // 高頻度で送る。250msだと反応が遅く感じる
}