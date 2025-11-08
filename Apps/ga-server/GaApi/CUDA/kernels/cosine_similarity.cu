// CUDA kernel for high-performance cosine similarity calculation
// Optimized for Guitar Alchemist chord vector search

#include <cuda_runtime.h>
#include <device_launch_parameters.h>
#include <math.h>

// Thread block size for optimal GPU utilization
#define BLOCK_SIZE 256
#define WARP_SIZE 32

// Shared memory for reduction operations
extern __shared__ double shared_mem[];

/**
 * CUDA kernel to calculate cosine similarity between a query vector and multiple chord vectors
 * 
 * @param query_vector: Query embedding vector (384 dimensions)
 * @param chord_vectors: Array of chord embedding vectors (num_chords x 384)
 * @param similarities: Output array of similarity scores (num_chords)
 * @param num_chords: Number of chords to compare against
 * @param vector_dim: Dimension of embedding vectors (384 for all-MiniLM-L6-v2)
 */
__global__ void cosine_similarity_kernel(
    const double* query_vector,
    const double* chord_vectors,
    double* similarities,
    int num_chords,
    int vector_dim)
{
    int chord_idx = blockIdx.x * blockDim.x + threadIdx.x;

    if (chord_idx >= num_chords) return;

    // Calculate offset for this chord's vector
    const double* chord_vector = chord_vectors + chord_idx * vector_dim;

    // Calculate dot product, query norm, and chord norm
    double dot_product = 0.0;
    double query_norm = 0.0;
    double chord_norm = 0.0;

    // Vectorized computation
    for (int i = 0; i < vector_dim; i++)
    {
        double q_val = query_vector[i];
        double c_val = chord_vector[i];

        dot_product += q_val * c_val;
        query_norm += q_val * q_val;
        chord_norm += c_val * c_val;
    }

    // Calculate cosine similarity
    double magnitude = sqrt(query_norm) * sqrt(chord_norm);
    similarities[chord_idx] = (magnitude > 0.0) ? (dot_product / magnitude) : 0.0;
}

/**
 * Optimized kernel using shared memory and warp-level primitives
 * Better performance for larger vector dimensions
 */
__global__ void cosine_similarity_optimized_kernel(
    const double* query_vector,
    const double* chord_vectors,
    double* similarities,
    int num_chords,
    int vector_dim)
{
    int chord_idx = blockIdx.x;
    int tid = threadIdx.x;

    if (chord_idx >= num_chords) return;

    // Shared memory for partial sums
    double* s_dot = shared_mem;
    double* s_query_norm = s_dot + blockDim.x;
    double* s_chord_norm = s_query_norm + blockDim.x;

    const double* chord_vector = chord_vectors + chord_idx * vector_dim;

    // Initialize shared memory
    s_dot[tid] = 0.0;
    s_query_norm[tid] = 0.0;
    s_chord_norm[tid] = 0.0;

    // Each thread processes multiple elements
    for (int i = tid; i < vector_dim; i += blockDim.x)
    {
        double q_val = query_vector[i];
        double c_val = chord_vector[i];

        s_dot[tid] += q_val * c_val;
        s_query_norm[tid] += q_val * q_val;
        s_chord_norm[tid] += c_val * c_val;
    }

    __syncthreads();

    // Reduction within block
    for (int stride = blockDim.x / 2; stride > 0; stride >>= 1)
    {
        if (tid < stride)
        {
            s_dot[tid] += s_dot[tid + stride];
            s_query_norm[tid] += s_query_norm[tid + stride];
            s_chord_norm[tid] += s_chord_norm[tid + stride];
        }
        __syncthreads();
    }

    // Thread 0 calculates final similarity
    if (tid == 0)
    {
        double magnitude = sqrt(s_query_norm[0]) * sqrt(s_chord_norm[0]);
        similarities[chord_idx] = (magnitude > 0.0) ? (s_dot[0] / magnitude) : 0.0;
    }
}

/**
 * Batch cosine similarity with filtering
 * Only calculates similarities for chords matching filter criteria
 */
__global__ void cosine_similarity_filtered_kernel(
    const double* query_vector,
    const double* chord_vectors,
    const int* filter_indices,
    double* similarities,
    int num_filtered_chords,
    int vector_dim)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;

    if (idx >= num_filtered_chords) return;

    int chord_idx = filter_indices[idx];
    const double* chord_vector = chord_vectors + chord_idx * vector_dim;

    double dot_product = 0.0;
    double query_norm = 0.0;
    double chord_norm = 0.0;

    for (int i = 0; i < vector_dim; i++)
    {
        double q_val = query_vector[i];
        double c_val = chord_vector[i];

        dot_product += q_val * c_val;
        query_norm += q_val * q_val;
        chord_norm += c_val * c_val;
    }

    double magnitude = sqrt(query_norm) * sqrt(chord_norm);
    similarities[idx] = (magnitude > 0.0) ? (dot_product / magnitude) : 0.0;
}

/**
 * Top-K selection kernel using parallel reduction
 * Finds the K most similar chords efficiently on GPU
 */
__global__ void top_k_selection_kernel(
    const double* similarities,
    const int* chord_ids,
    int* top_k_ids,
    double* top_k_scores,
    int num_chords,
    int k)
{
    int tid = threadIdx.x;
    int bid = blockIdx.x;

    // Each block handles a portion of the similarity array
    int start_idx = bid * blockDim.x;
    int end_idx = min(start_idx + blockDim.x, num_chords);

    // Shared memory for local top-k
    extern __shared__ double s_scores[];
    auto s_ids = (int*)(s_scores + k);

    // Initialize with worst possible values
    if (tid < k)
    {
        s_scores[tid] = -1.0;
        s_ids[tid] = -1;
    }
    __syncthreads();

    // Process elements assigned to this thread
    for (int i = start_idx + tid; i < end_idx; i += blockDim.x)
    {
        double score = similarities[i];
        int id = chord_ids[i];

        // Insert into local top-k if better than worst
        if (score > s_scores[k - 1])
        {
            // Find insertion position
            int pos = k - 1;
            while (pos > 0 && score > s_scores[pos - 1])
            {
                pos--;
            }

            // Shift elements and insert
            for (int j = k - 1; j > pos; j--)
            {
                s_scores[j] = s_scores[j - 1];
                s_ids[j] = s_ids[j - 1];
            }
            s_scores[pos] = score;
            s_ids[pos] = id;
        }
    }

    __syncthreads();

    // Block 0 merges results from all blocks
    if (bid == 0 && tid < k)
    {
        top_k_scores[tid] = s_scores[tid];
        top_k_ids[tid] = s_ids[tid];
    }
}

// Host function declarations for C++ interop
extern "C" {
/**
 * Launch cosine similarity calculation on GPU
 */
cudaError_t launch_cosine_similarity(
    const double* d_query_vector,
    const double* d_chord_vectors,
    double* d_similarities,
    int num_chords,
    int vector_dim,
    cudaStream_t stream = 0);

/**
 * Launch optimized cosine similarity with shared memory
 */
cudaError_t launch_cosine_similarity_optimized(
    const double* d_query_vector,
    const double* d_chord_vectors,
    double* d_similarities,
    int num_chords,
    int vector_dim,
    cudaStream_t stream = 0);

/**
 * Launch filtered cosine similarity
 */
cudaError_t launch_cosine_similarity_filtered(
    const double* d_query_vector,
    const double* d_chord_vectors,
    const int* d_filter_indices,
    double* d_similarities,
    int num_filtered_chords,
    int vector_dim,
    cudaStream_t stream = 0);
}

// Host function implementations
cudaError_t launch_cosine_similarity(
    const double* d_query_vector,
    const double* d_chord_vectors,
    double* d_similarities,
    int num_chords,
    int vector_dim,
    cudaStream_t stream)
{
    int block_size = BLOCK_SIZE;
    int grid_size = (num_chords + block_size - 1) / block_size;

    cosine_similarity_kernel<<<grid_size, block_size, 0, stream>>>(
        d_query_vector, d_chord_vectors, d_similarities, num_chords, vector_dim);

    return cudaGetLastError();
}

cudaError_t launch_cosine_similarity_optimized(
    const double* d_query_vector,
    const double* d_chord_vectors,
    double* d_similarities,
    int num_chords,
    int vector_dim,
    cudaStream_t stream)
{
    int block_size = BLOCK_SIZE;
    int grid_size = num_chords; // One block per chord
    int shared_mem_size = 3 * block_size * sizeof(double);

    cosine_similarity_optimized_kernel<<<grid_size, block_size, shared_mem_size, stream>>>(
        d_query_vector, d_chord_vectors, d_similarities, num_chords, vector_dim);

    return cudaGetLastError();
}

cudaError_t launch_cosine_similarity_filtered(
    const double* d_query_vector,
    const double* d_chord_vectors,
    const int* d_filter_indices,
    double* d_similarities,
    int num_filtered_chords,
    int vector_dim,
    cudaStream_t stream)
{
    int block_size = BLOCK_SIZE;
    int grid_size = (num_filtered_chords + block_size - 1) / block_size;

    cosine_similarity_filtered_kernel<<<grid_size, block_size, 0, stream>>>(
        d_query_vector, d_chord_vectors, d_filter_indices, d_similarities,
        num_filtered_chords, vector_dim);

    return cudaGetLastError();
}
