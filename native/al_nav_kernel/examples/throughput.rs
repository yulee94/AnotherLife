use al_nav_kernel::{
    al_nav_find_paths_v1, al_nav_scratch_words_v1, AlNavGridV1, AlNavPathResultV1, AlNavPointV1,
    AlNavQueryV1, AL_NAV_ABI_VERSION_V1, AL_NAV_STATUS_OK,
};
use std::env;
use std::hint::black_box;
use std::mem::size_of;
use std::time::{Duration, Instant};

fn main() {
    let arguments: Vec<String> = env::args().collect();
    let width = parse_argument(&arguments, 1, 72u32);
    let height = parse_argument(&arguments, 2, 72u32);
    let query_count = parse_argument(&arguments, 3, 192usize);
    let iterations = parse_argument(&arguments, 4, 20usize);
    assert!(width > 1 && height > 1, "width and height must exceed one");
    assert!(query_count > 0 && iterations > 0, "counts must be nonzero");

    let cell_count = (width as usize)
        .checked_mul(height as usize)
        .expect("synthetic grid dimensions overflowed usize");
    let cells = vec![1u8; cell_count];
    let queries = build_queries(width, height, query_count);
    let grid = AlNavGridV1 {
        struct_size: size_of::<AlNavGridV1>() as u32,
        abi_version: AL_NAV_ABI_VERSION_V1,
        width,
        height,
        cells: cells.as_ptr(),
        cells_len: cells.len(),
    };

    let mut scratch_word_count = 0usize;
    // SAFETY: scratch_word_count is valid writable storage.
    let scratch_status = unsafe { al_nav_scratch_words_v1(width, height, &mut scratch_word_count) };
    assert_eq!(scratch_status, AL_NAV_STATUS_OK);
    let mut scratch = vec![0u32; scratch_word_count];

    // The synthetic grid is open and uniformly weighted, so a shortest path has
    // at most width + height - 1 points.
    let points_per_query = width as usize + height as usize;
    let mut batch_points = vec![AlNavPointV1::default(); query_count * points_per_query];
    let mut batch_results = vec![AlNavPathResultV1::default(); query_count];
    run_batch(
        &grid,
        &queries,
        &mut batch_points,
        &mut batch_results,
        &mut scratch,
    );

    let (batch_duration, batch_checksum) = measure(iterations, || {
        run_batch(
            &grid,
            &queries,
            &mut batch_points,
            &mut batch_results,
            &mut scratch,
        )
    });

    let mut single_points = vec![AlNavPointV1::default(); points_per_query];
    let mut single_result = [AlNavPathResultV1::default(); 1];
    run_one_call_loop(
        &grid,
        &queries,
        &mut single_points,
        &mut single_result,
        &mut scratch,
    );
    let (loop_duration, loop_checksum) = measure(iterations, || {
        run_one_call_loop(
            &grid,
            &queries,
            &mut single_points,
            &mut single_result,
            &mut scratch,
        )
    });
    assert_eq!(
        batch_checksum, loop_checksum,
        "batched and per-query calls must produce identical semantic results"
    );

    let total_queries = query_count * iterations;
    let batch_rate = throughput(total_queries, batch_duration);
    let loop_rate = throughput(total_queries, loop_duration);
    println!("al_nav_kernel illustrative ABI throughput smoke");
    println!("host={} {}", env::consts::OS, env::consts::ARCH);
    println!(
        "grid={}x{}, queries/iteration={}, iterations={}, total_queries={}",
        width, height, query_count, iterations, total_queries
    );
    println!(
        "batch:    {:>9.1} queries/s ({:.3}s), checksum={}",
        batch_rate,
        batch_duration.as_secs_f64(),
        black_box(batch_checksum)
    );
    println!(
        "per-query:{:>9.1} queries/s ({:.3}s), checksum={}",
        loop_rate,
        loop_duration.as_secs_f64(),
        black_box(loop_checksum)
    );
    println!(
        "descriptive batch/per-query ratio: {:.3}x",
        batch_rate / loop_rate
    );
    println!(
        "This compares ABI call shapes only; it is not a claim against C#, Burst, Unity NavMesh, or server code."
    );
}

fn run_batch(
    grid: &AlNavGridV1,
    queries: &[AlNavQueryV1],
    points: &mut [AlNavPointV1],
    results: &mut [AlNavPathResultV1],
    scratch: &mut [u32],
) -> u64 {
    let written = execute_batch(grid, queries, points, results, scratch);
    semantic_checksum(&results[..queries.len()], points, written)
}

fn execute_batch(
    grid: &AlNavGridV1,
    queries: &[AlNavQueryV1],
    points: &mut [AlNavPointV1],
    results: &mut [AlNavPathResultV1],
    scratch: &mut [u32],
) -> usize {
    let mut written = 0usize;
    // SAFETY: all slices remain valid, aligned, and non-overlapping during the call.
    let status = unsafe {
        al_nav_find_paths_v1(
            grid,
            queries.as_ptr(),
            queries.len(),
            points.as_mut_ptr(),
            points.len(),
            results.as_mut_ptr(),
            results.len(),
            scratch.as_mut_ptr(),
            scratch.len(),
            &mut written,
        )
    };
    assert_eq!(status, AL_NAV_STATUS_OK);
    assert!(results[..queries.len()]
        .iter()
        .all(|result| result.status == AL_NAV_STATUS_OK));

    written
}

fn run_one_call_loop(
    grid: &AlNavGridV1,
    queries: &[AlNavQueryV1],
    points: &mut [AlNavPointV1],
    result: &mut [AlNavPathResultV1; 1],
    scratch: &mut [u32],
) -> u64 {
    let mut checksum = CHECKSUM_SEED;
    let mut total_written = 0usize;
    for query in queries {
        let written = execute_batch(grid, std::slice::from_ref(query), points, result, scratch);
        total_written += written;
        checksum = fold_result(checksum, &result[0], points);
    }
    black_box(mix(checksum, total_written as u64))
}

const CHECKSUM_SEED: u64 = 1_469_598_103_934_665_603;
const CHECKSUM_PRIME: u64 = 1_099_511_628_211;

fn semantic_checksum(
    results: &[AlNavPathResultV1],
    points: &[AlNavPointV1],
    written: usize,
) -> u64 {
    let checksum = results.iter().fold(CHECKSUM_SEED, |value, result| {
        fold_result(value, result, points)
    });
    black_box(mix(checksum, written as u64))
}

fn fold_result(mut checksum: u64, result: &AlNavPathResultV1, points: &[AlNavPointV1]) -> u64 {
    checksum = mix(checksum, u64::from(result.status));
    checksum = mix(checksum, result.total_cost);
    checksum = mix(checksum, result.visited_nodes);
    checksum = mix(checksum, result.point_count as u64);
    let end = result.point_offset + result.point_count;
    for point in &points[result.point_offset..end] {
        checksum = mix(checksum, u64::from(point.x));
        checksum = mix(checksum, u64::from(point.y));
    }
    checksum
}

fn mix(checksum: u64, value: u64) -> u64 {
    checksum.wrapping_mul(CHECKSUM_PRIME).wrapping_add(value)
}

fn measure(mut iterations: usize, mut operation: impl FnMut() -> u64) -> (Duration, u64) {
    let started = Instant::now();
    let mut checksum = 0u64;
    while iterations > 0 {
        checksum = checksum
            .rotate_left(7)
            .wrapping_add(operation())
            .wrapping_mul(1_099_511_628_211);
        iterations -= 1;
    }
    (started.elapsed(), black_box(checksum))
}

fn throughput(query_count: usize, duration: Duration) -> f64 {
    query_count as f64 / duration.as_secs_f64()
}

fn build_queries(width: u32, height: u32, count: usize) -> Vec<AlNavQueryV1> {
    let mut random = Lcg(0xa11c_e5ed_5eed_f00d);
    let mut queries = Vec::with_capacity(count);
    while queries.len() < count {
        let start_x = random.next() as u32 % width;
        let start_y = random.next() as u32 % height;
        let goal_x = random.next() as u32 % width;
        let goal_y = random.next() as u32 % height;
        if start_x == goal_x && start_y == goal_y {
            continue;
        }
        queries.push(AlNavQueryV1::new(start_x, start_y, goal_x, goal_y));
    }
    queries
}

fn parse_argument<T>(arguments: &[String], index: usize, default: T) -> T
where
    T: std::str::FromStr,
    T::Err: std::fmt::Display,
{
    arguments.get(index).map_or(default, |value| {
        value
            .parse::<T>()
            .unwrap_or_else(|error| panic!("invalid argument {index} ({value}): {error}"))
    })
}

struct Lcg(u64);

impl Lcg {
    fn next(&mut self) -> u64 {
        self.0 = self
            .0
            .wrapping_mul(6_364_136_223_846_793_005)
            .wrapping_add(1_442_695_040_888_963_407);
        self.0
    }
}
