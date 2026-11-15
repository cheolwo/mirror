"""홍달 HDIO v2용 건물 외곽·벽·그림자의 삼각화. 월드 평면은 동쪽 X·북쪽 Z다."""
import math
import struct

import numpy as np
from shapely import constrained_delaunay_triangles
from shapely.affinity import translate
from shapely.geometry import Polygon


def polygons(geometry):
    if isinstance(geometry, Polygon):
        if not geometry.is_empty and geometry.area > 0:
            yield geometry
    elif hasattr(geometry, "geoms"):
        for part in geometry.geoms:
            yield from polygons(part)


class Mesh:
    def __init__(self):
        self.xyz = []
        self.rgba = []
        self.uv = []
        self.uv2 = []
        self.indices = []

    def vertex(self, xyz, color, uv=(0,0), uv2=(0,0)):
        self.xyz.append(xyz)
        self.rgba.append((*color, 255))
        self.uv.append(uv)
        self.uv2.append(uv2)
        return len(self.xyz) - 1

    def flat(self, geometry, y, color, tag=0):
        """제약 들로네 삼각화. 빈 안뜰·섬 사이·오목한 외곽에 삼각형이 생기지 않는다."""
        for poly in polygons(geometry):
            covered = 0
            refs = {}
            for tri in constrained_delaunay_triangles(poly).geoms:
                covered += tri.area
                idx = []
                for x,z in list(tri.exterior.coords)[:3]:
                    if (x,z) not in refs:
                        refs[(x,z)] = self.vertex((x,y,z),color,(tag,0))
                    idx.append(refs[(x,z)])
                self.indices.extend(idx)
            if not math.isclose(covered, poly.area, rel_tol=2e-8, abs_tol=0.00002):
                raise ValueError(f"삼각형과 원본 면적 차이 {covered} {poly.area}")

    def quad(self, vertices, color, uvs, tag):
        base = len(self.xyz)
        for point, uv in zip(vertices, uvs):
            self.vertex(point, color, uv, tag)
        self.indices.extend(base+k for k in (0,1,2,0,2,3))

    def encode(self, colors):
        n = len(self.xyz)
        coords = np.asarray(self.xyz, dtype="<f4")
        idx = np.asarray(self.indices, dtype="<i4")
        assert 0 < n < 4_000_000 and 0 < idx.size < 12_000_000 and idx.size%3 == 0
        assert np.all(np.isfinite(coords)) and 0 <= idx.min() and idx.max() < n
        result = [struct.pack("<i",n),coords.tobytes()]
        if colors:
            result.extend(np.asarray(data, dtype=kind).tobytes() for data,kind in
                          [(self.rgba,"u1"),(self.uv,"<f4"),(self.uv2,"<f4")])
        result.extend([struct.pack("<i",idx.size), idx.tobytes()])
        return b"".join(result)


def ring_values(ring):
    return [round(value,5) for point in list(ring.coords)[:-1] for value in point]


def extrusion(mesh, shadow, footprint, height, retained, seed, style):
    palette = style["roofRgbPalette"]
    color = palette[seed % len(palette)]
    shade = min(retained, style["shadowMaxHeightMetres"]) * style["shadowScale"]
    light_x, light_z = style["shadowEast"], style["shadowNorth"]
    shadow_x, shadow_z = light_x * shade, light_z * shade
    shadow_y = style["shadowYMetres"]
    parapet = style["parapetMetres"]
    for poly in polygons(footprint):
        mesh.flat(poly,retained,color)
        shadow.flat(translate(poly,shadow_x,shadow_z),shadow_y,(0,0,0))
        for ring in [poly.exterior,*poly.interiors]:
            for i, (a,b) in enumerate(zip(ring.coords,list(ring.coords)[1:])):
                x,z = a
                xx,zz = b
                length = math.dist(a,b)
                if length < .000001: continue
                lighting = style["wallAmbient"] + style["wallDirectional"] * max(0, ((zz-z)*(-light_x)-(xx-x)*(-light_z))/length)
                c=tuple(round(v*lighting) for v in style["wallRgb"])
                mesh.quad([(x,0,z),(xx,0,zz),(xx,retained,zz),(x,retained,z)],c,
                          [(0,0),(length,0),(length,height),(0,height)],(1,(seed+17*i)%997))
                mesh.quad([(x,retained,z),(xx,retained,zz),(xx,retained+parapet,zz),(x,retained+parapet,z)],c,
                          [(0,0),(length,0),(length,parapet),(0,parapet)],(0,-1))
                shadow.quad([(x,shadow_y,z),(xx,shadow_y,zz),(xx+shadow_x,shadow_y,zz+shadow_z),
                             (x+shadow_x,shadow_y,z+shadow_z)],(0,0,0),[(0,0)]*4,(0,0))


