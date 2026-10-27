"""고정 추천 가정이 실패를 성공이나 StopIteration으로 숨기지 않는지 합성 검사한다."""
import importlib.util
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location('r108_workflow', Path(__file__).with_name('workflow.py'))
workflow = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(workflow)


class CurrentRecommendationTests(unittest.TestCase):
    def test_matching_current_offer(self):
        item = {'의뢰Id': 'synthetic-request', '추천라운드': 1}
        self.assertEqual(item, workflow.current_cargo_recommendation([item], 'synthetic-request'))

    def test_empty_list_is_explicit_held(self):
        with self.assertRaisesRegex(RuntimeError, 'CargoRecommendationHeld_NoCurrentOffer'):
            workflow.current_cargo_recommendation([], 'synthetic-request')

    def test_other_request_is_not_selected(self):
        with self.assertRaisesRegex(RuntimeError, 'CargoRecommendationHeld_NoCurrentOffer'):
            workflow.current_cargo_recommendation([{'의뢰Id': 'other'}], 'synthetic-request')

    def test_duplicate_offer_is_not_arbitrarily_selected(self):
        with self.assertRaisesRegex(RuntimeError, 'CargoRecommendationDuplicateRequest'):
            workflow.current_cargo_recommendation([{'의뢰Id': 'same'}, {'의뢰Id': 'same'}], 'same')

    def test_response_shape_is_checked(self):
        with self.assertRaisesRegex(RuntimeError, 'CargoRecommendationResponseShapeInvalid'):
            workflow.current_cargo_recommendation({'items': []}, 'synthetic-request')


if __name__ == '__main__':
    unittest.main()
