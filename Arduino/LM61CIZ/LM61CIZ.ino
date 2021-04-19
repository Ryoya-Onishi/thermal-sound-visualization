// File: LM61CIZ.ino
// Project: LM61CIZ
// Created Date: 19/04/2021
// Author: Shun Suzuki
// -----
// Last Modified: 19/04/2021
// Modified By: Shun Suzuki (suzuki@hapis.k.u-tokyo.ac.jp)
// -----
// Copyright (c) 2021 Hapis Lab. All rights reserved.
//

#define A_IN (0)
#define A_IN_MIN (0)
#define A_IN_MAX (1023)
#define V_MIN (0)
#define V_MAX (5000)
#define LM61CIZ_V_MIN (300)
#define LM61CIZ_V_MAX (1600)
#define LM61CIZ_T_MIN (-30)
#define LM61CIZ_T_MAX (100)

void setup() { Serial.begin(115200); }

float mapf(float v, float fromLow, float fromHigh, float toLow, float toHigh) {
  return (v - fromLow) * (toHigh - toLow) / (fromHigh - fromLow) + toLow;
}

void loop() {
  float a_in = analogRead(A_IN);
  float v = mapf(a_in, A_IN_MIN, A_IN_MAX, V_MIN, V_MAX);
  float t = mapf(v, LM61CIZ_V_MIN, LM61CIZ_V_MAX, LM61CIZ_T_MIN, LM61CIZ_T_MAX);
  Serial.println(t);
  delay(100);
}
