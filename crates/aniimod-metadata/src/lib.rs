//! aniimod-metadata — decrypt + parse Aniimo's `global-metadata.dat`.
//!
//! This is a lift of apiimo-core's battle-tested metadata recovery
//! (`../Apiimo_src`): the V2 keystream decrypt, the sanity-fixed layout,
//! and a read-only index over images → type definitions → fields/methods.
//!
//! Why it matters for the engine: with this plus the export map, a mod can
//! resolve ANY game method by `(image, namespace.class, method)` and get a
//! `*const MethodInfo` — replacing Cpp2IL, the prebaked DummyDlls, and the
//! Il2CppInterop assembly generator entirely.
//!
//! Milestone plan: M1 starts by porting the decrypt + header parse, then
//! string/table indexing, then typedef resolution. apiimo-core already has
//! every struct offset — port, don't re-derive.

/// Decrypt an encrypted global-metadata.dat blob in memory.
///
/// V2 layout: first 0x10..0x20 region sanity-marker, 1152-byte keystream
/// applied per the offsets recovered in apiimo (see
/// `Apiimo_src/apiimo-core/src/metadata` for the reference implementation).
pub fn decrypt(_encrypted: &[u8]) -> Result<Vec<u8>, MetadataError> {
    todo!("port apiimo-core V2 keystream decrypt (M1)")
}

#[derive(Debug)]
pub enum MetadataError {
    BadMagic,
    Truncated,
    UnsupportedVersion(u32),
}

/// Parsed, indexed view over a decrypted metadata blob.
pub struct Metadata {
    _blob: Vec<u8>,
}

impl Metadata {
    pub fn parse(decrypted: Vec<u8>) -> Result<Self, MetadataError> {
        if decrypted.len() < 4 || &decrypted[..4] != b"\xaf\x1b\xb1\xfa" {
            return Err(MetadataError::BadMagic);
        }
        Ok(Self { _blob: decrypted })
    }

    /// Resolve a method by image name + type name + method name.
    /// Returns the metadata method index (map to a live `MethodInfo*` via
    /// `il2cpp_class_get_method_from_name` in aniimod-ffi).
    pub fn find_method(
        &self,
        _image: &str,
        _type_name: &str,
        _method: &str,
    ) -> Result<u32, MetadataError> {
        todo!("port typedef/method index (M1)")
    }
}
