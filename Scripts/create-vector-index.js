// MongoDB Vector Search Index Creation Script
// Run this script with: mongosh < create-vector-index.js

// Connect to database
use('guitar-alchemist');

print('Creating vector search index for chord embeddings...');

// Check if index already exists
const existingIndexes = db.chords.getIndexes();
const vectorIndexExists = existingIndexes.some(idx => idx.name === 'chord_vector_index');

if (vectorIndexExists) {
    print('Vector index already exists. Dropping it first...');
    db.chords.dropIndex('chord_vector_index');
}

// Create vector search index
// Note: This requires MongoDB 8.0+ with vector search support
try {
    db.chords.createSearchIndex({
        name: "chord_vector_index",
        type: "vectorSearch",
        definition: {
            fields: [
                {
                    type: "vector",
                    path: "Embedding",
                    numDimensions: 384,  // For all-MiniLM-L6-v2 local model
                    similarity: "cosine"
                }
            ]
        }
    });

    print('✓ Vector search index created successfully!');
    print('Index name: chord_vector_index');
    print('Dimensions: 384 (all-MiniLM-L6-v2)');
    print('Similarity: cosine');
} catch (error) {
    print('Error creating vector index:');
    print(error.message);
    print('\nNote: Vector search requires MongoDB 8.0+ Community Edition');
    print('If you see an error, make sure you have MongoDB 8.0 or later installed.');
}

// Verify the index
print('\nVerifying index...');
const indexes = db.chords.getIndexes();
const vectorIndex = indexes.find(idx => idx.name === 'chord_vector_index');

if (vectorIndex) {
    print('✓ Index verified successfully!');
    printjson(vectorIndex);
} else {
    print('⚠ Index not found. Please check for errors above.');
}

// Check how many documents have embeddings
const totalDocs = db.chords.countDocuments();
const docsWithEmbeddings = db.chords.countDocuments({Embedding: {$exists: true}});

print('\nEmbedding Statistics:');
print(`Total chords: ${totalDocs}`);
print(`Chords with embeddings: ${docsWithEmbeddings}`);
print(`Chords without embeddings: ${totalDocs - docsWithEmbeddings}`);

if (docsWithEmbeddings === 0) {
    print('\n⚠ No embeddings found!');
    print('Please run the LocalEmbedding tool first to generate embeddings.');
    print('Command: dotnet run --project Apps/LocalEmbedding/LocalEmbedding.csproj');
}

print('\n✓ Setup complete!');

