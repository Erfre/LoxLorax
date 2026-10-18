"""Create a clean distributable; never include game DLLs, test plugins or logs."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import hashlib
import json

root=Path(__file__).resolve().parents[1]
version=json.loads((root/'manifest.json').read_text(encoding='utf-8'))['version_number']
out=root/'dist'/f'LoxLorax-{version}.zip'
files={
    'BepInEx/plugins/LoxLorax/LoxLorax.dll':root/'dist/LoxLorax/LoxLorax.dll',
    'BepInEx/plugins/LoxLorax/lorax.llmesh':root/'dist/LoxLorax/lorax.llmesh',
    'BepInEx/plugins/LoxLorax/vile-lorax.llmesh':root/'dist/LoxLorax/vile-lorax.llmesh',
    'README.md':root/'README.md',
}
assert files['BepInEx/plugins/LoxLorax/lorax.llmesh'].read_bytes()==(root/'assets/lorax.llmesh').read_bytes(), 'Rebuild to copy the latest mesh first'
assert files['BepInEx/plugins/LoxLorax/vile-lorax.llmesh'].read_bytes()==(root/'assets/vile/lorax.llmesh').read_bytes(), 'Rebuild to copy the latest Vile mesh first'
with ZipFile(out,'w',ZIP_DEFLATED,compresslevel=9) as archive:
    for name,path in files.items():
        assert path.is_file(), path
        archive.write(path,name)
with ZipFile(out) as archive:
    assert archive.testzip() is None
    assert set(archive.namelist())==set(files)
    for name,path in files.items(): assert archive.read(name)==path.read_bytes()
digest=hashlib.sha256(out.read_bytes()).hexdigest()
out.with_suffix('.zip.sha256').write_text(f'{digest}  {out.name}\n')
print(f'PASS: packaged and verified {out} ({out.stat().st_size:,} bytes)')
