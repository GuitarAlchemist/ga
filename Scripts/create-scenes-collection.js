// MongoDB script to create 'ga' database and 'scenes' collection
// Usage: mongosh < Scripts/create-scenes-collection.js

// Switch to 'ga' database (creates if doesn't exist)
use ga;

// Insert document into 'scenes' collection (creates collection if doesn't exist)
const insertResult = db.scenes.insertOne({
  sceneId: "demo1",
  file: "public/scene.glb",
  verts: 12034,
  sizeKB: 742
});

print("\n=== INSERT RESULT ===");
print("Inserted document with _id: " + insertResult.insertedId);

// Find document by sceneId
const findResult = db.scenes.findOne({ sceneId: "demo1" });

print("\n=== FIND RESULT ===");
printjson(findResult);

// Show collection stats
print("\n=== COLLECTION INFO ===");
print("Database: ga");
print("Collection: scenes");
print("Document count: " + db.scenes.countDocuments());

