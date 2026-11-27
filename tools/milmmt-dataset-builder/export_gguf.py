#!/usr/bin/env python3
"""Export a merged MiLMMT-46 (Gemma3) HuggingFace checkpoint to BF16 GGUF.

Uses llama.cpp's official Gemma3Model converter for all tensor transforms (including
Gemma3 RMSNorm +1.0 shift and architecture metadata), while copying the exact
SentencePiece tokenizer metadata (`tokenizer.ggml.*`) from an official reference
MiLMMT GGUF so tokenization matches 100% bit-for-bit (`add_bos_token=False`,
`add_space_prefix=False`, 262,144 tokens/scores/types).
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser(description="Export merged MiLMMT-46 HF dir to BF16 GGUF.")
    parser.add_argument("--merged-dir", type=Path, required=True)
    parser.add_argument("--ref-gguf", type=Path, required=True, help="Official MiLMMT GGUF to copy exact vocab from.")
    parser.add_argument("--outfile", type=Path, required=True)
    parser.add_argument("--outtype", default="bf16", choices=["bf16", "f16", "f32", "q8_0"])
    parser.add_argument("--llama-cpp-dir", type=Path, default=Path(r"D:\REPO\llama.cpp"))
    parser.add_argument("--model-name", default="MiLMMT-46-1B-v1.0-ffxiv")
    args = parser.parse_args()

    sys.path.insert(0, str(args.llama_cpp_dir / "gguf-py"))
    sys.path.insert(0, str(args.llama_cpp_dir))
    import gguf
    from conversion.gemma import Gemma3Model
    from conversion.base import ModelBase

    ref = gguf.GGUFReader(str(args.ref_gguf))
    ref_tokens = ref.fields["tokenizer.ggml.tokens"].contents()
    ref_scores = ref.fields["tokenizer.ggml.scores"].contents()
    ref_types = ref.fields["tokenizer.ggml.token_type"].contents()
    ref_model = ref.fields["tokenizer.ggml.model"].contents()
    ref_pre = ref.fields["tokenizer.ggml.pre"].contents() if "tokenizer.ggml.pre" in ref.fields else "default"
    ref_bos = int(ref.fields["tokenizer.ggml.bos_token_id"].contents())
    ref_eos = int(ref.fields["tokenizer.ggml.eos_token_id"].contents())
    ref_unk = int(ref.fields["tokenizer.ggml.unknown_token_id"].contents())
    ref_pad = int(ref.fields["tokenizer.ggml.padding_token_id"].contents())
    ref_add_bos = bool(ref.fields["tokenizer.ggml.add_bos_token"].contents())
    ref_add_eos = bool(ref.fields["tokenizer.ggml.add_eos_token"].contents())
    ref_add_space = bool(ref.fields["tokenizer.ggml.add_space_prefix"].contents())
    ref_chat_tmpl = (
        ref.fields["tokenizer.chat_template"].contents() if "tokenizer.chat_template" in ref.fields else None
    )

    class MiLMMTGemma3Model(Gemma3Model):
        model_arch = gguf.MODEL_ARCH.GEMMA3

        def set_vocab(self):
            self.gguf_writer.add_tokenizer_model(ref_model)
            self.gguf_writer.add_tokenizer_pre(ref_pre)
            self.gguf_writer.add_token_list(ref_tokens)
            self.gguf_writer.add_token_scores(ref_scores)
            self.gguf_writer.add_token_types(ref_types)
            self.gguf_writer.add_bos_token_id(ref_bos)
            self.gguf_writer.add_eos_token_id(ref_eos)
            self.gguf_writer.add_unk_token_id(ref_unk)
            self.gguf_writer.add_pad_token_id(ref_pad)
            self.gguf_writer.add_add_bos_token(ref_add_bos)
            self.gguf_writer.add_add_eos_token(ref_add_eos)
            self.gguf_writer.add_add_space_prefix(ref_add_space)
            if ref_chat_tmpl is not None:
                self.gguf_writer.add_chat_template(ref_chat_tmpl)

        def modify_tensors(self, data_torch, name: str, bid: int | None):
            if "embed_tokens.weight" in name and data_torch.shape[0] > len(ref_tokens):
                data_torch = data_torch[: len(ref_tokens)]
            f_shift = self.norm_shift(name)
            if f_shift != 0.0:
                data_torch = data_torch + f_shift
            yield from super(Gemma3Model, self).modify_tensors(data_torch, name, bid)

    ftype_map = {
        "f32": gguf.LlamaFileType.ALL_F32,
        "f16": gguf.LlamaFileType.MOSTLY_F16,
        "bf16": gguf.LlamaFileType.MOSTLY_BF16,
        "q8_0": gguf.LlamaFileType.MOSTLY_Q8_0,
    }

    hparams = ModelBase.load_hparams(args.merged_dir, is_mistral_format=False)
    args.outfile.parent.mkdir(parents=True, exist_ok=True)
    converter = MiLMMTGemma3Model(
        dir_model=args.merged_dir,
        ftype=ftype_map[args.outtype],
        fname_out=args.outfile,
        is_big_endian=False,
        use_temp_file=False,
        eager=False,
        metadata_override=None,
        model_name=args.model_name,
        split_max_tensors=0,
        split_max_size=0,
        dry_run=False,
        small_first_shard=False,
        hparams=hparams,
    )
    converter.write()
    print(f"Exported GGUF successfully: {args.outfile} ({args.outfile.stat().st_size:,} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
