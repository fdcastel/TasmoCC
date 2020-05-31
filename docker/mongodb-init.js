rs.initiate();

print("Waiting for replica initialization...");
sleep(5000);
print("Done! Replica status is " + rs.status().ok);
