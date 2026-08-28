#ifndef AL_NAV_KERNEL_H
#define AL_NAV_KERNEL_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32) && !defined(AL_NAV_STATIC)
#if defined(AL_NAV_EXPORTS)
#define AL_NAV_API __declspec(dllexport)
#else
#define AL_NAV_API __declspec(dllimport)
#endif
#elif defined(__GNUC__) || defined(__clang__)
#define AL_NAV_API __attribute__((visibility("default")))
#else
#define AL_NAV_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define AL_NAV_ABI_VERSION_V1 UINT32_C(0x00010000)
#define AL_NAV_SCRATCH_WORDS_PER_CELL_V1 UINT32_C(4)

typedef uint32_t AlNavStatusV1;

enum {
    AL_NAV_STATUS_OK = 0,
    AL_NAV_STATUS_INVALID_ARGUMENT = 1,
    AL_NAV_STATUS_ABI_MISMATCH = 2,
    AL_NAV_STATUS_DIMENSION_OVERFLOW = 3,
    AL_NAV_STATUS_GRID_TOO_LARGE = 4,
    AL_NAV_STATUS_BUFFER_TOO_SMALL = 5,
    AL_NAV_STATUS_MISALIGNED_BUFFER = 6,
    AL_NAV_STATUS_UNSUPPORTED_FLAGS = 7,
    AL_NAV_STATUS_START_OUT_OF_BOUNDS = 8,
    AL_NAV_STATUS_GOAL_OUT_OF_BOUNDS = 9,
    AL_NAV_STATUS_START_BLOCKED = 10,
    AL_NAV_STATUS_GOAL_BLOCKED = 11,
    AL_NAV_STATUS_NO_PATH = 12,
    AL_NAV_STATUS_POINT_BUFFER_TOO_SMALL = 13,
    AL_NAV_STATUS_COST_OVERFLOW = 14,
    AL_NAV_STATUS_INTERNAL_ERROR = 254,
    AL_NAV_STATUS_PANIC = 255
};

enum {
    AL_NAV_QUERY_FLAGS_NONE_V1 = 0
};

/*
 * ABI structures are native in-memory call structures, not serialized records.
 * Set each input struct_size to sizeof(the exact v1 structure). Query flags must
 * be zero. The kernel writes result.reserved as zero. A grid cell is blocked at
 * 0 and otherwise contains the integer cost (1..255) charged when entered.
 */
typedef struct AlNavGridV1 {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t width;
    uint32_t height;
    const uint8_t *cells;
    size_t cells_len;
} AlNavGridV1;

typedef struct AlNavQueryV1 {
    uint32_t struct_size;
    uint32_t flags;
    uint32_t start_x;
    uint32_t start_y;
    uint32_t goal_x;
    uint32_t goal_y;
} AlNavQueryV1;

typedef struct AlNavPointV1 {
    uint32_t x;
    uint32_t y;
} AlNavPointV1;

typedef struct AlNavPathResultV1 {
    AlNavStatusV1 status;
    uint32_t reserved;
    size_t point_offset;
    size_t point_count;
    uint64_t total_cost;
    uint64_t visited_nodes;
} AlNavPathResultV1;

/* Returns AL_NAV_ABI_VERSION_V1. This export has no fallible work. */
AL_NAV_API uint32_t al_nav_abi_version_v1(void);

/*
 * Reports the required uint32_t scratch capacity for a width x height grid.
 * out_scratch_words is required and is set to zero before validation.
 */
AL_NAV_API AlNavStatusV1 al_nav_scratch_words_v1(
    uint32_t width,
    uint32_t height,
    size_t *out_scratch_words);

/*
 * Executes queries sequentially with no retained state or internal allocation.
 *
 * Structural failures (bad pointers/alignment, ABI, grid length, result/scratch
 * capacity) are returned directly. Once structure is valid, the function returns
 * AL_NAV_STATUS_OK and writes one result per query. Query failures are recorded in
 * AlNavPathResultV1.status and do not stop later queries.
 *
 * outputs may be NULL only when outputs_capacity is zero. queries/results may be
 * NULL only when query_count is zero. out_points_written is always required.
 * scratch must contain at least al_nav_scratch_words_v1(width, height) naturally
 * aligned uint32_t words when query_count is nonzero.
 *
 * Every nonzero-length region must be valid, naturally aligned for its declared
 * type, and non-overlapping with every mutable output/scratch region. The grid and
 * queries are read-only and no pointer is retained after return. No declared
 * region may exceed PTRDIFF_MAX bytes; an oversized length is invalid.
 */
AL_NAV_API AlNavStatusV1 al_nav_find_paths_v1(
    const AlNavGridV1 *grid,
    const AlNavQueryV1 *queries,
    size_t query_count,
    AlNavPointV1 *outputs,
    size_t outputs_capacity,
    AlNavPathResultV1 *results,
    size_t results_capacity,
    uint32_t *scratch,
    size_t scratch_words_capacity,
    size_t *out_points_written);

#ifdef __cplusplus
}
#endif

#endif /* AL_NAV_KERNEL_H */
