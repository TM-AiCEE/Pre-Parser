import csv

# generate a list of maps, each map item is like:
# {
#   'Cards': '7C,7D'
#   'Board': '8C,KH,QS,TH'
#   'Action': 'check'
#   'AverageRank': '19.2367992401123'
#   'Count': '1'
# }
with open('parsed.csv') as f:
    parsed = [{k: v for k, v in row.items()}
              for row in csv.DictReader(f, skipinitialspace=True)]

# generate a map from Cards+Board to Action:
# {
#   '7C,7D;8C,KH,QS,TH': 'check'
# }
decision_from_parsed = {row['Cards'] + ';' + row['Board']: row['Action']
                        for row in parsed}

def get_parsed_decision(cards, board):
    k = cards + ';' board
    return decision_from_parsed[k] if k in decision_from_parsed else None
