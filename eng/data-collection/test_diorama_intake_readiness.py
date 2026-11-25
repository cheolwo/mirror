"""실제 관측/게임 수요와 원본/동별 소속을 혼동하지 않는 입력 관문 회귀."""
import importlib.util
import gzip
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('diorama_intake', Path(__file__).with_name('verify_diorama_intake_readiness.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class DioramaInputBoundaryTests(unittest.TestCase):
    def test_edited_feature_count_cannot_be_reported_as_metadata_count(self):
        with tempfile.TemporaryDirectory() as name:
            path=Path(name)/'points.geojson.gz'
            with gzip.open(path,'wt',encoding='utf-8') as stream:
                json.dump({'features':[{'id':'one'}]},stream)
            with self.assertRaisesRegex(module.InputRejected,'FeatureCountMismatch'):
                module.압축피처읽기(path,2,'tree-test')

    def test_other_dong_metadata_and_missing_supplement_cannot_pass(self):
        index={'name':'동1','district':'구1'}
        bundle={'code':'동1','name':'동1','district':'구1'}
        supplement={'administrativeDongCode':'동1','name':'동1','parent':'구1',
                    'supplements':{'officialStreetFurniture':{},'officialWalkingNetwork':{}}}
        module.지역메타데이터대조('동1',index,bundle,supplement)
        with self.assertRaisesRegex(module.InputRejected,'DongMetadataIdentityMismatch'):
            module.지역메타데이터대조('동1',index,{**bundle,'code':'동2'},supplement)
        with self.assertRaisesRegex(module.InputRejected,'MissingExplicitSupplementState'):
            module.지역메타데이터대조('동1',index,bundle,{**supplement,'supplements':{}})

    def test_unpinned_metadata_cannot_supply_its_own_accepted_hash(self):
        with tempfile.TemporaryDirectory() as name:
            path=Path(name)/'bundle.json'; path.write_text('{}',encoding='utf-8')
            with self.assertRaisesRegex(module.InputRejected,'UnpinnedInputMetadata'):
                module.고정JSON읽기(path,{})

    def test_changed_bytes_rejected_even_when_file_length_stays_equal(self):
        with tempfile.TemporaryDirectory() as name:
            root = Path(name); path = root / 'source.bin'; path.write_bytes(b'first')
            reference = {'path': 'source.bin', 'sha256': module.digest(path), 'bytes': 5}
            path.write_bytes(b'other')
            with self.assertRaisesRegex(module.InputRejected, 'ReferencedShaMismatch'):
                module.verify_reference(root, reference, {})

    def test_missing_source_does_not_become_empty_layer(self):
        with tempfile.TemporaryDirectory() as name:
            with self.assertRaisesRegex(module.InputRejected, 'MissingReferencedFile'):
                module.verify_reference(Path(name), {'path': 'not-received.zip', 'sha256': '0'*64}, {})

    def test_extra_or_missing_stage_members_block_complete_partition_claim(self):
        with self.assertRaisesRegex(module.InputRejected, 'StagePartitionCountsMismatch'):
            module.선택지역({'a': 0, 'b': 1}, [1, 2], 1)

    def test_collection_stage_is_cumulative(self):
        self.assertEqual(module.선택지역({'a': 0, 'b': 1, 'c': 2}, [1, 1, 1], 1), ['a', 'b'])

    def test_cross_dong_fragments_remain_distinct_memberships(self):
        self.assertEqual(module.소속수량([['one', 'two'], ['two', 'three'], ['two']]),
                         {'membershipRows': 5, 'uniqueSourceFeatures': 3, 'crossDongSourceFeatures': 1})

    def test_ready_observations_do_not_open_missing_game_demand(self):
        gate = module.수요관문(['existing', 'new'], {
            'existing': {'adminObservationRows': 22, 'currentGameDemandPresent': True},
            'new': {'adminObservationRows': 22, 'currentGameDemandPresent': False}})
        self.assertTrue(gate['observationsPreparedForEveryRequestedDong'])
        self.assertEqual(gate['missingGameDemandCodes'], ['new'])
        self.assertFalse(gate['runtimeUnlockAuthorized'])
        self.assertFalse(gate['observationToOrderModelApproved'])

    def test_unknown_observations_are_not_filled_with_zero_or_other_dong(self):
        gate = module.수요관문(['unknown'], {})
        self.assertFalse(gate['observationsPreparedForEveryRequestedDong'])
        self.assertEqual(gate['missingGameDemandCodes'], ['unknown'])

    def test_existing_output_rejected_before_source_processing(self):
        with tempfile.TemporaryDirectory() as name:
            root = Path(name); output = root / 'artifacts/local/existing'; output.mkdir(parents=True)
            with self.assertRaisesRegex(module.InputRejected, 'ExistingOutputNotOverwritten'):
                module.run(root, {}, 1, output)

    def test_output_cannot_write_outside_local_artifacts(self):
        with tempfile.TemporaryDirectory() as name:
            root = Path(name)
            with self.assertRaisesRegex(module.InputRejected, 'OutputMustStayWithinLocalArtifacts'):
                module.run(root, {}, 1, root/'product/new-folder')


if __name__ == '__main__':
    unittest.main()
