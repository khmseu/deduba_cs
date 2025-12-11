#!/bin/bash
# Print entries from the copilot-chat rows, converting CSV-quoted MD fields:
# - If the MD field is surrounded by double quotes, strip the outer quotes
#   and replace doubled double-quotes (CSV escaping) with a single quote.
while IFS=, read -r en p v trn tn _ip _is md; do
	# remove a trailing carriage return (\r) artifact if present
	md=${md%$'\r'}
	# If md starts and ends with a double-quote, unquote and un-escape CSV-style double-quotes
	# echo "MD field before processing: [${md}]"
	if [[ ${md} == \"*\" ]]; then
		# remove first and last character (the outer quotes)
		md="${md:1:${#md}-2}"
		# replace doubled double-quotes with a single double-quote (use sed to avoid tricky escaping)
		md=$(printf '%s' "${md}" | sed 's/""/"/g')
	fi
	# echo "MD field after processing:  [${md}]"

	echo "### ${trn:-${tn}}"
	echo
	echo '```text'
	# Use -e to interpret escaped control sequences (\n, etc.) produced by the CSV generator
	echo -e "${md}" | fold -s
	echo '```'
	echo
	echo "  -- ${p}/${en}@${v}"
	echo
done <.github/copilot-languageModelTools.csv >.github/copilot-languageModelTools.md
