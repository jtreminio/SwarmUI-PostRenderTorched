import os
import shlex
from dataclasses import dataclass, field

import numpy as np


@dataclass
class CubeLUT:
    table: np.ndarray
    name: str
    domain: np.ndarray
    comments: list[str] = field(default_factory=list)


def _parse_cube(lut_path: str) -> CubeLUT:
    domain_min = np.array([0.0, 0.0, 0.0], dtype=np.float32)
    domain_max = np.array([1.0, 1.0, 1.0], dtype=np.float32)
    dimensions = None
    size = None
    data = []
    comments = []

    with open(lut_path, encoding="utf-8") as cube_file:
        for raw_line in cube_file:
            line = raw_line.strip()

            if not line:
                continue

            if line.startswith("#"):
                comments.append(line[1:].strip())
                continue

            tokens = shlex.split(line, comments=False, posix=True)
            if not tokens:
                continue

            keyword = tokens[0]
            if keyword == "TITLE":
                continue
            if keyword == "DOMAIN_MIN":
                if len(tokens) != 4:
                    raise ValueError(f"Invalid DOMAIN_MIN line in LUT: {lut_path}")
                domain_min = np.asarray(tokens[1:], dtype=np.float32)
                continue
            if keyword == "DOMAIN_MAX":
                if len(tokens) != 4:
                    raise ValueError(f"Invalid DOMAIN_MAX line in LUT: {lut_path}")
                domain_max = np.asarray(tokens[1:], dtype=np.float32)
                continue
            if keyword == "LUT_1D_SIZE":
                if len(tokens) != 2:
                    raise ValueError(f"Invalid LUT_1D_SIZE line in LUT: {lut_path}")
                dimensions = 2
                size = int(tokens[1])
                continue
            if keyword == "LUT_3D_SIZE":
                if len(tokens) != 2:
                    raise ValueError(f"Invalid LUT_3D_SIZE line in LUT: {lut_path}")
                dimensions = 3
                size = int(tokens[1])
                continue

            if len(tokens) != 3:
                raise ValueError(f"Invalid LUT data row in LUT: {lut_path}")
            data.append([float(value) for value in tokens])

    if dimensions is None or size is None:
        raise ValueError(f"Missing LUT size declaration in LUT: {lut_path}")

    table = np.asarray(data, dtype=np.float32)
    expected_rows = size if dimensions == 2 else size**3
    if table.shape != (expected_rows, 3):
        raise ValueError(
            f"Unexpected LUT data size in {lut_path}: expected {(expected_rows, 3)}, got {table.shape}"
        )

    if dimensions == 3:
        # IRIDAS .cube stores rows with red changing fastest and blue slowest.
        table = np.reshape(table, (size, size, size, 3), order="F")

    return CubeLUT(
        table=table,
        name=os.path.splitext(os.path.basename(lut_path))[0],
        domain=np.vstack([domain_min, domain_max]),
        comments=comments,
    )


def read_lut(lut_path, clip=False):
    """
    Reads a LUT from the specified path, returning a parsed 1D or 3D LUT.

    <lut_path>: the path to the file from which to read the LUT (
    <clip>: flag indicating whether to apply clipping of LUT values, limiting all values to the domain's lower and
        upper bounds
    """
    lut = _parse_cube(lut_path)

    if clip:
        if lut.domain[0].max() == lut.domain[0].min() and lut.domain[1].max() == lut.domain[1].min():
            lut.table = np.clip(lut.table, lut.domain[0, 0], lut.domain[1, 0])
        else:
            if len(lut.table.shape) == 2:  # 3x1D
                for dim in range(3):
                    lut.table[:, dim] = np.clip(lut.table[:, dim], lut.domain[0, dim], lut.domain[1, dim])
            else:  # 3D
                for dim in range(3):
                    lut.table[:, :, :, dim] = np.clip(lut.table[:, :, :, dim], lut.domain[0, dim], lut.domain[1, dim])

    return lut
