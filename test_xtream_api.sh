#!/bin/bash

# Xtream API Test Script
BASE_URL="http://webhop.xyz:8080"
USERNAME="Srinivas67"
PASSWORD="6729282"  # Pass password as first argument

echo "Testing Xtream API..."
echo "===================="
echo ""

# Test 1: Get user and server info
echo "1. Testing user/server info:"
curl -s "${BASE_URL}/player_api.php?username=${USERNAME}&password=${PASSWORD}" | jq '.' || echo "Failed to parse JSON"
echo ""
echo ""

# Test 2: Get series categories
echo "2. Testing series categories:"
curl -s "${BASE_URL}/player_api.php?username=${USERNAME}&password=${PASSWORD}&action=get_series_categories" | jq '.' || echo "Failed to parse JSON"
echo ""
echo ""

# Test 3: Get series by category (use first category)
echo "3. Testing series list (category 1):"
curl -s "${BASE_URL}/player_api.php?username=${USERNAME}&password=${PASSWORD}&action=get_series&category_id=1" | jq '.[0]' || echo "Failed to parse JSON"
echo ""
echo ""

# Test 4: Get series info for a specific series (you'll need to provide series_id)
if [ ! -z "$2" ]; then
    echo "4. Testing series info for series_id=$2:"
    curl -s "${BASE_URL}/player_api.php?username=${USERNAME}&password=${PASSWORD}&action=get_series_info&series_id=$2" | jq '.' || echo "Failed to parse JSON"
fi
