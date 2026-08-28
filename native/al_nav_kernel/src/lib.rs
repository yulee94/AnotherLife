//! Deterministic, allocation-free weighted-grid path queries behind a C ABI.
//!
//! ABI v1 uses four-way movement, integer entry costs, a fixed node-index tie
//! break, and caller-owned memory. The implementation retains no caller pointer
//! and owns no mutable process-wide gameplay state.

#![forbid(unsafe_op_in_unsafe_fn)]

use std::mem::{align_of, size_of};
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::slice;

pub const AL_NAV_ABI_VERSION_V1: u32 = 0x0001_0000;
pub const AL_NAV_SCRATCH_WORDS_PER_CELL_V1: usize = 4;

pub const AL_NAV_STATUS_OK: u32 = 0;
pub const AL_NAV_STATUS_INVALID_ARGUMENT: u32 = 1;
pub const AL_NAV_STATUS_ABI_MISMATCH: u32 = 2;
pub const AL_NAV_STATUS_DIMENSION_OVERFLOW: u32 = 3;
pub const AL_NAV_STATUS_GRID_TOO_LARGE: u32 = 4;
pub const AL_NAV_STATUS_BUFFER_TOO_SMALL: u32 = 5;
pub const AL_NAV_STATUS_MISALIGNED_BUFFER: u32 = 6;
pub const AL_NAV_STATUS_UNSUPPORTED_FLAGS: u32 = 7;
pub const AL_NAV_STATUS_START_OUT_OF_BOUNDS: u32 = 8;
pub const AL_NAV_STATUS_GOAL_OUT_OF_BOUNDS: u32 = 9;
pub const AL_NAV_STATUS_START_BLOCKED: u32 = 10;
pub const AL_NAV_STATUS_GOAL_BLOCKED: u32 = 11;
pub const AL_NAV_STATUS_NO_PATH: u32 = 12;
pub const AL_NAV_STATUS_POINT_BUFFER_TOO_SMALL: u32 = 13;
pub const AL_NAV_STATUS_COST_OVERFLOW: u32 = 14;
pub const AL_NAV_STATUS_INTERNAL_ERROR: u32 = 254;
pub const AL_NAV_STATUS_PANIC: u32 = 255;

pub const AL_NAV_QUERY_FLAGS_NONE_V1: u32 = 0;

const UNSET_NODE: u32 = u32::MAX;
const INFINITE_COST: u32 = u32::MAX;

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct AlNavGridV1 {
    pub struct_size: u32,
    pub abi_version: u32,
    pub width: u32,
    pub height: u32,
    pub cells: *const u8,
    pub cells_len: usize,
}

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct AlNavQueryV1 {
    pub struct_size: u32,
    pub flags: u32,
    pub start_x: u32,
    pub start_y: u32,
    pub goal_x: u32,
    pub goal_y: u32,
}

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct AlNavPointV1 {
    pub x: u32,
    pub y: u32,
}

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct AlNavPathResultV1 {
    pub status: u32,
    pub reserved: u32,
    pub point_offset: usize,
    pub point_count: usize,
    pub total_cost: u64,
    pub visited_nodes: u64,
}

impl AlNavQueryV1 {
    pub fn new(start_x: u32, start_y: u32, goal_x: u32, goal_y: u32) -> Self {
        Self {
            struct_size: size_of::<Self>() as u32,
            flags: AL_NAV_QUERY_FLAGS_NONE_V1,
            start_x,
            start_y,
            goal_x,
            goal_y,
        }
    }
}

#[no_mangle]
pub extern "C" fn al_nav_abi_version_v1() -> u32 {
    AL_NAV_ABI_VERSION_V1
}

/// Computes the scratch capacity required by ABI v1.
///
/// # Safety
/// `out_scratch_words` must point to writable, naturally aligned `usize`
/// storage for the duration of the call.
#[no_mangle]
pub unsafe extern "C" fn al_nav_scratch_words_v1(
    width: u32,
    height: u32,
    out_scratch_words: *mut usize,
) -> u32 {
    ffi_guard(|| {
        if out_scratch_words.is_null() {
            return AL_NAV_STATUS_INVALID_ARGUMENT;
        }
        if !is_aligned(out_scratch_words) {
            return AL_NAV_STATUS_MISALIGNED_BUFFER;
        }

        // SAFETY: the caller contract and checks above require writable aligned storage.
        unsafe { *out_scratch_words = 0 };
        match required_scratch_words(width, height) {
            Ok(words) => {
                // SAFETY: validated above and no aliasing reference is retained.
                unsafe { *out_scratch_words = words };
                AL_NAV_STATUS_OK
            }
            Err(status) => status,
        }
    })
}

/// Executes a deterministic query batch using only caller-owned memory.
///
/// # Safety
/// Every pointer with a nonzero associated length must be valid and naturally
/// aligned for its declared type. Mutable output and scratch regions must not
/// overlap each other or any input region. `grid` and `out_points_written` are
/// always required. No pointer may be invalidated during the call.
#[no_mangle]
#[allow(clippy::too_many_arguments)]
pub unsafe extern "C" fn al_nav_find_paths_v1(
    grid: *const AlNavGridV1,
    queries: *const AlNavQueryV1,
    query_count: usize,
    outputs: *mut AlNavPointV1,
    outputs_capacity: usize,
    results: *mut AlNavPathResultV1,
    results_capacity: usize,
    scratch: *mut u32,
    scratch_words_capacity: usize,
    out_points_written: *mut usize,
) -> u32 {
    ffi_guard(|| {
        // SAFETY: this is the single raw-pointer boundary. The implementation
        // validates null/alignment/capacity before constructing slices.
        unsafe {
            find_paths_impl(
                grid,
                queries,
                query_count,
                outputs,
                outputs_capacity,
                results,
                results_capacity,
                scratch,
                scratch_words_capacity,
                out_points_written,
            )
        }
    })
}

fn ffi_guard(operation: impl FnOnce() -> u32) -> u32 {
    match catch_unwind(AssertUnwindSafe(operation)) {
        Ok(status) => status,
        Err(_) => AL_NAV_STATUS_PANIC,
    }
}

#[allow(clippy::too_many_arguments)]
unsafe fn find_paths_impl(
    grid_pointer: *const AlNavGridV1,
    queries_pointer: *const AlNavQueryV1,
    query_count: usize,
    outputs_pointer: *mut AlNavPointV1,
    outputs_capacity: usize,
    results_pointer: *mut AlNavPathResultV1,
    results_capacity: usize,
    scratch_pointer: *mut u32,
    scratch_words_capacity: usize,
    out_points_written: *mut usize,
) -> u32 {
    if out_points_written.is_null() {
        return AL_NAV_STATUS_INVALID_ARGUMENT;
    }
    if !is_aligned(out_points_written) {
        return AL_NAV_STATUS_MISALIGNED_BUFFER;
    }
    // SAFETY: checked non-null and aligned; the caller promises writable storage.
    unsafe { *out_points_written = 0 };

    let (grid, cells, cell_count) = match unsafe { validate_grid(grid_pointer) } {
        Ok(validated) => validated,
        Err(status) => return status,
    };

    if outputs_capacity > 0 {
        if outputs_pointer.is_null() {
            return AL_NAV_STATUS_INVALID_ARGUMENT;
        }
        if !is_aligned(outputs_pointer) {
            return AL_NAV_STATUS_MISALIGNED_BUFFER;
        }
        if !is_valid_slice_length::<AlNavPointV1>(outputs_capacity) {
            return AL_NAV_STATUS_INVALID_ARGUMENT;
        }
    }
    if query_count == 0 {
        return AL_NAV_STATUS_OK;
    }
    if queries_pointer.is_null() || results_pointer.is_null() || scratch_pointer.is_null() {
        return AL_NAV_STATUS_INVALID_ARGUMENT;
    }
    if !is_aligned(queries_pointer) || !is_aligned(results_pointer) || !is_aligned(scratch_pointer)
    {
        return AL_NAV_STATUS_MISALIGNED_BUFFER;
    }
    if results_capacity < query_count {
        return AL_NAV_STATUS_BUFFER_TOO_SMALL;
    }
    if !is_valid_slice_length::<AlNavQueryV1>(query_count)
        || !is_valid_slice_length::<AlNavPathResultV1>(query_count)
    {
        return AL_NAV_STATUS_INVALID_ARGUMENT;
    }

    let required_scratch = match cell_count.checked_mul(AL_NAV_SCRATCH_WORDS_PER_CELL_V1) {
        Some(words) => words,
        None => return AL_NAV_STATUS_DIMENSION_OVERFLOW,
    };
    if scratch_words_capacity < required_scratch {
        return AL_NAV_STATUS_BUFFER_TOO_SMALL;
    }

    // SAFETY: null/alignment/capacity checks are complete and the caller contract
    // requires valid, non-overlapping regions for the duration of this call.
    let queries = unsafe { slice::from_raw_parts(queries_pointer, query_count) };
    let results = unsafe { slice::from_raw_parts_mut(results_pointer, query_count) };
    let scratch = unsafe { slice::from_raw_parts_mut(scratch_pointer, required_scratch) };
    let outputs: &mut [AlNavPointV1] = if outputs_capacity == 0 {
        &mut []
    } else {
        // SAFETY: validated above and caller promises outputs_capacity writable entries.
        unsafe { slice::from_raw_parts_mut(outputs_pointer, outputs_capacity) }
    };

    let (distances, rest) = scratch.split_at_mut(cell_count);
    let (predecessors, rest) = rest.split_at_mut(cell_count);
    let (heap_nodes, heap_positions) = rest.split_at_mut(cell_count);
    let mut workspace = Workspace {
        distances,
        predecessors,
        heap_nodes,
        heap_positions,
        heap_len: 0,
    };

    let mut points_written = 0usize;
    for (query, result) in queries.iter().zip(results.iter_mut()) {
        *result = AlNavPathResultV1::default();
        if query.struct_size as usize != size_of::<AlNavQueryV1>() {
            result.status = AL_NAV_STATUS_ABI_MISMATCH;
            continue;
        }
        if query.flags != AL_NAV_QUERY_FLAGS_NONE_V1 {
            result.status = AL_NAV_STATUS_UNSUPPORTED_FLAGS;
            continue;
        }

        let (start, goal) = match validate_query(&grid, cells, query) {
            Ok(indices) => indices,
            Err(status) => {
                result.status = status;
                continue;
            }
        };

        let report = match workspace.search(
            cells,
            grid.width as usize,
            grid.height as usize,
            start,
            goal,
        ) {
            Ok(report) => report,
            Err(status) => {
                result.status = status;
                continue;
            }
        };
        result.visited_nodes = report.visited_nodes;
        if !report.found {
            result.status = AL_NAV_STATUS_NO_PATH;
            continue;
        }

        result.point_offset = points_written;
        result.point_count = report.path_len;
        result.total_cost = u64::from(report.total_cost);
        let available = outputs.len().saturating_sub(points_written);
        if report.path_len > available {
            result.status = AL_NAV_STATUS_POINT_BUFFER_TOO_SMALL;
            continue;
        }

        if let Err(status) = workspace.write_path(
            start,
            goal,
            grid.width as usize,
            &mut outputs[points_written..points_written + report.path_len],
        ) {
            result.status = status;
            continue;
        }
        result.status = AL_NAV_STATUS_OK;
        points_written += report.path_len;
    }

    // SAFETY: checked once at entry and no reference to this storage is retained.
    unsafe { *out_points_written = points_written };
    AL_NAV_STATUS_OK
}

unsafe fn validate_grid<'a>(
    grid_pointer: *const AlNavGridV1,
) -> Result<(AlNavGridV1, &'a [u8], usize), u32> {
    if grid_pointer.is_null() {
        return Err(AL_NAV_STATUS_INVALID_ARGUMENT);
    }
    if !is_aligned(grid_pointer) {
        return Err(AL_NAV_STATUS_MISALIGNED_BUFFER);
    }

    // SAFETY: pointer alignment/non-null are checked; validity is part of the C contract.
    let grid = unsafe { *grid_pointer };
    if grid.struct_size as usize != size_of::<AlNavGridV1>()
        || grid.abi_version != AL_NAV_ABI_VERSION_V1
    {
        return Err(AL_NAV_STATUS_ABI_MISMATCH);
    }

    let cell_count = checked_cell_count(grid.width, grid.height)?;
    if grid.cells_len != cell_count || grid.cells.is_null() {
        return Err(AL_NAV_STATUS_INVALID_ARGUMENT);
    }

    // SAFETY: the caller promises cells_len readable bytes and no invalidation.
    let cells = unsafe { slice::from_raw_parts(grid.cells, cell_count) };
    Ok((grid, cells, cell_count))
}

fn validate_query(
    grid: &AlNavGridV1,
    cells: &[u8],
    query: &AlNavQueryV1,
) -> Result<(usize, usize), u32> {
    if query.start_x >= grid.width || query.start_y >= grid.height {
        return Err(AL_NAV_STATUS_START_OUT_OF_BOUNDS);
    }
    if query.goal_x >= grid.width || query.goal_y >= grid.height {
        return Err(AL_NAV_STATUS_GOAL_OUT_OF_BOUNDS);
    }

    let width = grid.width as usize;
    let start = query.start_y as usize * width + query.start_x as usize;
    let goal = query.goal_y as usize * width + query.goal_x as usize;
    if cells[start] == 0 {
        return Err(AL_NAV_STATUS_START_BLOCKED);
    }
    if cells[goal] == 0 {
        return Err(AL_NAV_STATUS_GOAL_BLOCKED);
    }
    Ok((start, goal))
}

fn required_scratch_words(width: u32, height: u32) -> Result<usize, u32> {
    checked_cell_count(width, height)?
        .checked_mul(AL_NAV_SCRATCH_WORDS_PER_CELL_V1)
        .ok_or(AL_NAV_STATUS_DIMENSION_OVERFLOW)
}

fn checked_cell_count(width: u32, height: u32) -> Result<usize, u32> {
    if width == 0 || height == 0 {
        return Err(AL_NAV_STATUS_INVALID_ARGUMENT);
    }
    let count = u64::from(width)
        .checked_mul(u64::from(height))
        .ok_or(AL_NAV_STATUS_DIMENSION_OVERFLOW)?;
    if count > usize::MAX as u64 {
        return Err(AL_NAV_STATUS_DIMENSION_OVERFLOW);
    }
    if count >= u64::from(UNSET_NODE) {
        return Err(AL_NAV_STATUS_GRID_TOO_LARGE);
    }

    let maximum_cost = count
        .saturating_sub(1)
        .checked_mul(u64::from(u8::MAX))
        .ok_or(AL_NAV_STATUS_COST_OVERFLOW)?;
    if maximum_cost >= u64::from(INFINITE_COST) {
        return Err(AL_NAV_STATUS_COST_OVERFLOW);
    }
    Ok(count as usize)
}

fn is_aligned<T>(pointer: *const T) -> bool {
    (pointer as usize) % align_of::<T>() == 0
}

fn is_valid_slice_length<T>(length: usize) -> bool {
    let element_size = size_of::<T>();
    element_size == 0 || length <= isize::MAX as usize / element_size
}

struct SearchReport {
    found: bool,
    path_len: usize,
    total_cost: u32,
    visited_nodes: u64,
}

struct Workspace<'a> {
    distances: &'a mut [u32],
    predecessors: &'a mut [u32],
    heap_nodes: &'a mut [u32],
    heap_positions: &'a mut [u32],
    heap_len: usize,
}

impl Workspace<'_> {
    fn search(
        &mut self,
        cells: &[u8],
        width: usize,
        height: usize,
        start: usize,
        goal: usize,
    ) -> Result<SearchReport, u32> {
        self.reset();
        self.distances[start] = 0;
        self.push_or_decrease(start)?;
        let mut visited_nodes = 0u64;

        while let Some(current) = self.pop_min() {
            visited_nodes += 1;
            if current == goal {
                let path_len = self.path_len(start, goal)?;
                return Ok(SearchReport {
                    found: true,
                    path_len,
                    total_cost: self.distances[goal],
                    visited_nodes,
                });
            }

            let x = current % width;
            let y = current / width;
            // Fixed north, west, east, south ordering. Equal heap costs then
            // prefer the smaller row-major node index.
            if y > 0 {
                self.relax(cells, current, current - width)?;
            }
            if x > 0 {
                self.relax(cells, current, current - 1)?;
            }
            if x + 1 < width {
                self.relax(cells, current, current + 1)?;
            }
            if y + 1 < height {
                self.relax(cells, current, current + width)?;
            }
        }

        Ok(SearchReport {
            found: false,
            path_len: 0,
            total_cost: 0,
            visited_nodes,
        })
    }

    fn reset(&mut self) {
        self.distances.fill(INFINITE_COST);
        self.predecessors.fill(UNSET_NODE);
        self.heap_positions.fill(UNSET_NODE);
        self.heap_len = 0;
    }

    fn relax(&mut self, cells: &[u8], current: usize, neighbor: usize) -> Result<(), u32> {
        let entry_cost = cells[neighbor];
        if entry_cost == 0 {
            return Ok(());
        }

        let candidate = self.distances[current]
            .checked_add(u32::from(entry_cost))
            .ok_or(AL_NAV_STATUS_COST_OVERFLOW)?;
        if candidate < self.distances[neighbor] {
            self.distances[neighbor] = candidate;
            self.predecessors[neighbor] = current as u32;
            self.push_or_decrease(neighbor)?;
        }
        Ok(())
    }

    fn push_or_decrease(&mut self, node: usize) -> Result<(), u32> {
        let current_position = self.heap_positions[node];
        if current_position == UNSET_NODE {
            if self.heap_len >= self.heap_nodes.len() {
                return Err(AL_NAV_STATUS_INTERNAL_ERROR);
            }
            let position = self.heap_len;
            self.heap_len += 1;
            self.heap_nodes[position] = node as u32;
            self.heap_positions[node] = position as u32;
            self.sift_up(position);
        } else {
            self.sift_up(current_position as usize);
        }
        Ok(())
    }

    fn pop_min(&mut self) -> Option<usize> {
        if self.heap_len == 0 {
            return None;
        }

        let root = self.heap_nodes[0] as usize;
        self.heap_len -= 1;
        self.heap_positions[root] = UNSET_NODE;
        if self.heap_len > 0 {
            let replacement = self.heap_nodes[self.heap_len] as usize;
            self.heap_nodes[0] = replacement as u32;
            self.heap_positions[replacement] = 0;
            self.sift_down(0);
        }
        Some(root)
    }

    fn sift_up(&mut self, mut position: usize) {
        while position > 0 {
            let parent = (position - 1) / 2;
            if !self.node_precedes(self.heap_nodes[position], self.heap_nodes[parent]) {
                break;
            }
            self.swap_heap(position, parent);
            position = parent;
        }
    }

    fn sift_down(&mut self, mut position: usize) {
        loop {
            let left = position * 2 + 1;
            if left >= self.heap_len {
                return;
            }
            let right = left + 1;
            let mut best = left;
            if right < self.heap_len
                && self.node_precedes(self.heap_nodes[right], self.heap_nodes[left])
            {
                best = right;
            }
            if !self.node_precedes(self.heap_nodes[best], self.heap_nodes[position]) {
                return;
            }
            self.swap_heap(position, best);
            position = best;
        }
    }

    fn node_precedes(&self, left: u32, right: u32) -> bool {
        let left_cost = self.distances[left as usize];
        let right_cost = self.distances[right as usize];
        left_cost < right_cost || (left_cost == right_cost && left < right)
    }

    fn swap_heap(&mut self, left_position: usize, right_position: usize) {
        self.heap_nodes.swap(left_position, right_position);
        let left_node = self.heap_nodes[left_position] as usize;
        let right_node = self.heap_nodes[right_position] as usize;
        self.heap_positions[left_node] = left_position as u32;
        self.heap_positions[right_node] = right_position as u32;
    }

    fn path_len(&self, start: usize, goal: usize) -> Result<usize, u32> {
        let mut cursor = goal;
        let mut length = 1usize;
        while cursor != start {
            let predecessor = self.predecessors[cursor];
            if predecessor == UNSET_NODE {
                return Err(AL_NAV_STATUS_INTERNAL_ERROR);
            }
            cursor = predecessor as usize;
            length = length.checked_add(1).ok_or(AL_NAV_STATUS_INTERNAL_ERROR)?;
            if length > self.predecessors.len() {
                return Err(AL_NAV_STATUS_INTERNAL_ERROR);
            }
        }
        Ok(length)
    }

    fn write_path(
        &self,
        start: usize,
        goal: usize,
        width: usize,
        output: &mut [AlNavPointV1],
    ) -> Result<(), u32> {
        let expected = self.path_len(start, goal)?;
        if output.len() != expected {
            return Err(AL_NAV_STATUS_INTERNAL_ERROR);
        }

        let mut cursor = goal;
        let mut output_index = output.len();
        loop {
            output_index -= 1;
            output[output_index] = AlNavPointV1 {
                x: (cursor % width) as u32,
                y: (cursor / width) as u32,
            };
            if cursor == start {
                break;
            }
            let predecessor = self.predecessors[cursor];
            if predecessor == UNSET_NODE {
                return Err(AL_NAV_STATUS_INTERNAL_ERROR);
            }
            cursor = predecessor as usize;
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn grid(cells: &[u8], width: u32, height: u32) -> AlNavGridV1 {
        AlNavGridV1 {
            struct_size: size_of::<AlNavGridV1>() as u32,
            abi_version: AL_NAV_ABI_VERSION_V1,
            width,
            height,
            cells: cells.as_ptr(),
            cells_len: cells.len(),
        }
    }

    fn invoke(
        cells: &[u8],
        width: u32,
        height: u32,
        queries: &[AlNavQueryV1],
        output_capacity: usize,
    ) -> (u32, Vec<AlNavPointV1>, Vec<AlNavPathResultV1>, usize) {
        let mut scratch_words = 0usize;
        // SAFETY: scratch_words is valid writable storage.
        let scratch_status = unsafe { al_nav_scratch_words_v1(width, height, &mut scratch_words) };
        assert_eq!(scratch_status, AL_NAV_STATUS_OK);
        let mut scratch = vec![0u32; scratch_words];
        let mut outputs = vec![AlNavPointV1::default(); output_capacity];
        let mut results = vec![AlNavPathResultV1::default(); queries.len()];
        let mut written = usize::MAX;
        let grid = grid(cells, width, height);
        // SAFETY: all slices remain alive, aligned, and non-overlapping for the call.
        let status = unsafe {
            al_nav_find_paths_v1(
                &grid,
                queries.as_ptr(),
                queries.len(),
                if outputs.is_empty() {
                    std::ptr::null_mut()
                } else {
                    outputs.as_mut_ptr()
                },
                outputs.len(),
                if results.is_empty() {
                    std::ptr::null_mut()
                } else {
                    results.as_mut_ptr()
                },
                results.len(),
                scratch.as_mut_ptr(),
                scratch.len(),
                &mut written,
            )
        };
        outputs.truncate(written.min(outputs.len()));
        (status, outputs, results, written)
    }

    #[test]
    fn abi_and_scratch_contract_are_stable() {
        assert_eq!(al_nav_abi_version_v1(), 0x0001_0000);
        let mut words = 999usize;
        // SAFETY: words is valid writable storage.
        let status = unsafe { al_nav_scratch_words_v1(7, 5, &mut words) };
        assert_eq!(status, AL_NAV_STATUS_OK);
        assert_eq!(words, 7 * 5 * AL_NAV_SCRATCH_WORDS_PER_CELL_V1);

        let mut invalid_words = 999usize;
        // SAFETY: invalid_words is valid; invalid dimensions are ordinary input.
        let invalid = unsafe { al_nav_scratch_words_v1(0, 5, &mut invalid_words) };
        assert_eq!(invalid, AL_NAV_STATUS_INVALID_ARGUMENT);
        assert_eq!(invalid_words, 0);
    }

    #[test]
    fn weighted_path_uses_lower_total_entry_cost() {
        let cells = [1, 9, 1, 1, 1, 1];
        let query = AlNavQueryV1::new(0, 0, 2, 0);
        let (status, points, results, written) = invoke(&cells, 3, 2, &[query], 6);
        assert_eq!(status, AL_NAV_STATUS_OK);
        assert_eq!(written, 5);
        assert_eq!(results[0].status, AL_NAV_STATUS_OK);
        assert_eq!(results[0].total_cost, 4);
        assert_eq!(
            points,
            vec![
                AlNavPointV1 { x: 0, y: 0 },
                AlNavPointV1 { x: 0, y: 1 },
                AlNavPointV1 { x: 1, y: 1 },
                AlNavPointV1 { x: 2, y: 1 },
                AlNavPointV1 { x: 2, y: 0 },
            ]
        );
    }

    #[test]
    fn batch_records_query_failures_without_stopping_later_queries() {
        let cells = [1, 1, 1, 1, 0, 1, 1, 1, 1];
        let queries = [
            AlNavQueryV1::new(0, 0, 2, 2),
            AlNavQueryV1::new(1, 1, 2, 2),
            AlNavQueryV1::new(9, 0, 2, 2),
            AlNavQueryV1::new(2, 2, 2, 2),
        ];
        let (status, points, results, written) = invoke(&cells, 3, 3, &queries, 16);
        assert_eq!(status, AL_NAV_STATUS_OK);
        assert_eq!(results[0].status, AL_NAV_STATUS_OK);
        assert_eq!(results[1].status, AL_NAV_STATUS_START_BLOCKED);
        assert_eq!(results[2].status, AL_NAV_STATUS_START_OUT_OF_BOUNDS);
        assert_eq!(results[3].status, AL_NAV_STATUS_OK);
        assert_eq!(results[3].point_count, 1);
        assert_eq!(written, results[0].point_count + 1);
        assert_eq!(points[results[3].point_offset], AlNavPointV1 { x: 2, y: 2 });
    }

    #[test]
    fn undersized_point_buffer_writes_no_partial_path() {
        let cells = [1; 5];
        let query = AlNavQueryV1::new(0, 0, 4, 0);
        let (status, points, results, written) = invoke(&cells, 5, 1, &[query], 4);
        assert_eq!(status, AL_NAV_STATUS_OK);
        assert_eq!(results[0].status, AL_NAV_STATUS_POINT_BUFFER_TOO_SMALL);
        assert_eq!(results[0].point_offset, 0);
        assert_eq!(results[0].point_count, 5);
        assert_eq!(written, 0);
        assert!(points.is_empty());
    }

    #[test]
    fn structural_scratch_shortage_is_fail_closed() {
        let cells = [1; 9];
        let grid = grid(&cells, 3, 3);
        let query = AlNavQueryV1::new(0, 0, 2, 2);
        let mut output = [AlNavPointV1::default(); 9];
        let mut result = AlNavPathResultV1 {
            status: 99,
            ..AlNavPathResultV1::default()
        };
        let mut scratch = [0u32; 35];
        let mut written = 99usize;
        // SAFETY: buffers are valid but scratch is deliberately one word short.
        let status = unsafe {
            al_nav_find_paths_v1(
                &grid,
                &query,
                1,
                output.as_mut_ptr(),
                output.len(),
                &mut result,
                1,
                scratch.as_mut_ptr(),
                scratch.len(),
                &mut written,
            )
        };
        assert_eq!(status, AL_NAV_STATUS_BUFFER_TOO_SMALL);
        assert_eq!(written, 0);
        assert_eq!(result.status, 99);
    }

    #[test]
    fn misaligned_scratch_is_rejected_before_dereference() {
        let cells = [1; 4];
        let grid = grid(&cells, 2, 2);
        let query = AlNavQueryV1::new(0, 0, 1, 1);
        let mut output = [AlNavPointV1::default(); 4];
        let mut result = AlNavPathResultV1::default();
        let mut bytes = vec![0u8; 4 * AL_NAV_SCRATCH_WORDS_PER_CELL_V1 * 4 + 1];
        // SAFETY: adding one stays within bytes; the kernel must reject before dereference.
        let misaligned = unsafe { bytes.as_mut_ptr().add(1).cast::<u32>() };
        let mut written = 0usize;
        // SAFETY: every region except deliberately misaligned scratch is valid.
        let status = unsafe {
            al_nav_find_paths_v1(
                &grid,
                &query,
                1,
                output.as_mut_ptr(),
                output.len(),
                &mut result,
                1,
                misaligned,
                4 * AL_NAV_SCRATCH_WORDS_PER_CELL_V1,
                &mut written,
            )
        };
        assert_eq!(status, AL_NAV_STATUS_MISALIGNED_BUFFER);
        assert_eq!(written, 0);
    }

    #[test]
    fn impossible_output_slice_length_is_rejected_before_construction() {
        let cells = [1; 4];
        let grid = grid(&cells, 2, 2);
        let query = AlNavQueryV1::new(0, 0, 1, 1);
        let mut result = AlNavPathResultV1 {
            status: 99,
            ..AlNavPathResultV1::default()
        };
        let mut scratch = [0u32; 4 * AL_NAV_SCRATCH_WORDS_PER_CELL_V1];
        let mut written = 99usize;
        let impossible_capacity = isize::MAX as usize / size_of::<AlNavPointV1>() + 1;
        // A dangling pointer is sufficient here because validation must reject
        // the impossible length before constructing or accessing the slice.
        let dangling_output = std::ptr::NonNull::<AlNavPointV1>::dangling().as_ptr();
        // SAFETY: the deliberately non-dereferenceable output is paired with an
        // impossible capacity that must be rejected before any memory access.
        let status = unsafe {
            al_nav_find_paths_v1(
                &grid,
                &query,
                1,
                dangling_output,
                impossible_capacity,
                &mut result,
                1,
                scratch.as_mut_ptr(),
                scratch.len(),
                &mut written,
            )
        };
        assert_eq!(status, AL_NAV_STATUS_INVALID_ARGUMENT);
        assert_eq!(written, 0);
        assert_eq!(result.status, 99);
    }

    #[test]
    fn panic_guard_never_unwinds_through_the_boundary() {
        let status = ffi_guard(|| panic!("synthetic contained panic"));
        assert_eq!(status, AL_NAV_STATUS_PANIC);
    }

    #[test]
    fn generated_small_grids_match_slow_reference_and_repeat_exactly() {
        let mut random = Lcg::new(0x5eed_cafe_f00d_beef);
        for _case in 0..160 {
            let width = 3 + random.range(6) as u32;
            let height = 3 + random.range(6) as u32;
            let cell_count = width as usize * height as usize;
            let mut cells = Vec::with_capacity(cell_count);
            for _ in 0..cell_count {
                let sample = random.range(10);
                cells.push(if sample < 2 {
                    0
                } else {
                    1 + random.range(7) as u8
                });
            }

            let start = random.range(cell_count as u64) as usize;
            let goal = random.range(cell_count as u64) as usize;
            cells[start] = 1 + random.range(7) as u8;
            cells[goal] = 1 + random.range(7) as u8;
            let query = AlNavQueryV1::new(
                (start % width as usize) as u32,
                (start / width as usize) as u32,
                (goal % width as usize) as u32,
                (goal / width as usize) as u32,
            );

            let reference =
                slow_reference_cost(&cells, width as usize, height as usize, start, goal);
            let first = invoke(&cells, width, height, &[query], cell_count);
            let second = invoke(&cells, width, height, &[query], cell_count);
            assert_eq!(first.0, AL_NAV_STATUS_OK);
            assert_eq!(first.0, second.0);
            assert_eq!(first.1, second.1);
            assert_eq!(first.2, second.2);
            assert_eq!(first.3, second.3);

            match reference {
                Some(expected_cost) => {
                    assert_eq!(first.2[0].status, AL_NAV_STATUS_OK);
                    assert_eq!(first.2[0].total_cost, u64::from(expected_cost));
                    validate_path(&cells, width as usize, start, goal, &first.1, expected_cost);
                }
                None => {
                    assert_eq!(first.2[0].status, AL_NAV_STATUS_NO_PATH);
                    assert!(first.1.is_empty());
                }
            }
        }
    }

    fn validate_path(
        cells: &[u8],
        width: usize,
        start: usize,
        goal: usize,
        path: &[AlNavPointV1],
        expected_cost: u32,
    ) {
        assert!(!path.is_empty());
        assert_eq!(path[0].y as usize * width + path[0].x as usize, start);
        let last = path[path.len() - 1];
        assert_eq!(last.y as usize * width + last.x as usize, goal);

        let mut cost = 0u32;
        for pair in path.windows(2) {
            let dx = pair[0].x.abs_diff(pair[1].x);
            let dy = pair[0].y.abs_diff(pair[1].y);
            assert_eq!(dx + dy, 1);
            let index = pair[1].y as usize * width + pair[1].x as usize;
            assert_ne!(cells[index], 0);
            cost += u32::from(cells[index]);
        }
        assert_eq!(cost, expected_cost);
    }

    fn slow_reference_cost(
        cells: &[u8],
        width: usize,
        height: usize,
        start: usize,
        goal: usize,
    ) -> Option<u32> {
        let mut distances = vec![u32::MAX; cells.len()];
        let mut used = vec![false; cells.len()];
        distances[start] = 0;

        loop {
            let current = (0..cells.len())
                .filter(|&index| !used[index] && distances[index] != u32::MAX)
                .min_by_key(|&index| (distances[index], index));
            let current = current?;
            if current == goal {
                return Some(distances[current]);
            }
            used[current] = true;
            let x = current % width;
            let y = current / width;
            let neighbors = [
                if y > 0 { Some(current - width) } else { None },
                if x > 0 { Some(current - 1) } else { None },
                if x + 1 < width {
                    Some(current + 1)
                } else {
                    None
                },
                if y + 1 < height {
                    Some(current + width)
                } else {
                    None
                },
            ];
            for neighbor in neighbors.into_iter().flatten() {
                if cells[neighbor] == 0 || used[neighbor] {
                    continue;
                }
                let candidate = distances[current] + u32::from(cells[neighbor]);
                distances[neighbor] = distances[neighbor].min(candidate);
            }
        }
    }

    struct Lcg(u64);

    impl Lcg {
        fn new(seed: u64) -> Self {
            Self(seed)
        }

        fn range(&mut self, upper_exclusive: u64) -> u64 {
            self.0 = self
                .0
                .wrapping_mul(6_364_136_223_846_793_005)
                .wrapping_add(1_442_695_040_888_963_407);
            self.0 % upper_exclusive
        }
    }
}
