rs.initiate();

print("Waiting for replica initialization...");
sleep(5000);
print("Done! Replica status is " + rs.status().ok);

db.template.insertMany([
  {
    _id: "Sonoff Mini",
    definition: "{\"NAME\":\"Sonoff Mini\",\"GPIO\":[17,0,0,0,9,0,0,0,21,56,0,0,255],\"FLAG\":0,\"BASE\":1}",
    thumbnailUrl: "https://static.ubnt.com/fingerprint/0/3154_51x51.png",
    imageUrl: "https://static.ubnt.com/fingerprint/0/3154_257x257.png",
  },
  {
    _id: "Sonoff Basic",
    definition: "{\"NAME\":\"Sonoff Basic\",\"GPIO\":[17,255,255,255,255,0,0,0,21,56,255,0,0],\"FLAG\":0,\"BASE\":1}",
    thumbnailUrl: "https://static.ubnt.com/fingerprint/0/3805_51x51.png",
    imageUrl: "https://static.ubnt.com/fingerprint/0/3805_257x257.png",
  },
  {
    _id: "Sonoff BasicR3",
    definition: "{\"NAME\":\"Sonoff BasicR3\",\"GPIO\":[17,255,0,255,255,0,0,0,21,56,255,0,255],\"FLAG\":0,\"BASE\":1}",
    thumbnailUrl: "https://static.ubnt.com/fingerprint/0/3805_51x51.png",
    imageUrl: "https://static.ubnt.com/fingerprint/0/3805_257x257.png",
  },
  {
    _id: "Sonoff 4CHPro2",
    definition: "{\"NAME\":\"Sonoff 4CHPro2\",\"GPIO\":[17,255,255,255,23,22,18,19,21,56,20,24,0],\"FLAG\":0,\"BASE\":23}",
    thumbnailUrl: "https://static.ubnt.com/fingerprint/0/3743_51x51.png",
    imageUrl: "https://static.ubnt.com/fingerprint/0/3743_257x257.png",
  },
  {
    _id: "Sonoff Dual R2",
    definition: "{\"NAME\":\"Sonoff Dual R2\",\"GPIO\":[255,255,0,255,0,22,255,17,21,56,0,0,0],\"FLAG\":0,\"BASE\":39}",
    thumbnailUrl: "https://static.ubnt.com/fingerprint/0/3875_51x51.png",
    imageUrl: "https://static.ubnt.com/fingerprint/0/3875_257x257.png",
  },
]);

db.deviceConfiguration.insertMany([
  {
    _id: "*",
    setupCommands: "Time 0; TimeZone -3; Latitude -30.036163; Longitude -51.166354; HostName 1; SetOption53 1; Emulation 2"
  },
  {
    _id: "d8:f1:5b:b2:bb:a1",
    friendlyNames: ["Luz-Externa-Fundos"],
    topicName: "SON-Externa-Fundos",
    templateName: "Sonoff Mini",
  },
  {
    _id: "2c:f4:32:a8:01:67",
    friendlyNames: [
      "Janela-Video-A",
      "Janela-Video-F",
      "Cortina-Video-A",
      "Cortina-Video-F",
    ],
    topicName: "SON-Janela-Video",
    templateName: "Sonoff 4CHPro2",
  },
  {
    _id: "dc:4f:22:ac:fb:d6",
    friendlyNames: ["Cortina-Suite-A", "Cortina-Suite-F"],
    topicName: "SON-Cortina-Suite",
    templateName: "Sonoff Dual R2",
  },
  {
    _id: "d8:f1:5b:90:78:5f",
    friendlyNames: ["Relay1"],
    topicName: "SON-Relay1",
    templateName: "Sonoff Basic",
  },
  {
    _id: "d8:f1:5b:90:05:ec",
    friendlyNames: ["Relay2"],
    disabled: true,
    topicName: "SON-Relay2",
    templateName: "Sonoff Basic",
  },
  {
    _id: "dc:4f:22:aa:7e:97",
    friendlyNames: ["Relay3"],
    topicName: "SON-Relay3",
    templateName: "Sonoff BasicR3",
  },
]);
