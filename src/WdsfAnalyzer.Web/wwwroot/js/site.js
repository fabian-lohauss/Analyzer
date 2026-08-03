document.querySelectorAll('[data-analysis-form]').forEach((form) => {
	form.addEventListener('submit', (event) => {
		if (form.dataset.submitting === 'true') {
			event.preventDefault();
			return;
		}

		if (!form.checkValidity()) return;

		form.dataset.submitting = 'true';
		document.querySelector('[data-loading-layer]')?.removeAttribute('hidden');
		document.body.classList.add('is-loading');
		document.querySelectorAll('[data-analysis-form] button').forEach((button) => {
			button.setAttribute('aria-disabled', 'true');
		});
	});
});

const matrix = document.querySelector('.judge-matrix');
if (matrix) {
		const rows = [...matrix.querySelectorAll('[data-judge-row]')];
		const body = matrix.tBodies[0];
		const summaryHeading = matrix.querySelector('[data-summary-heading]');
		const filterBar = document.querySelector('[data-matrix-filter]');
		const judgeStatus = filterBar?.querySelector('[data-judge-filter-status]');
		const judgeLabel = filterBar?.querySelector('[data-judge-filter-label]');
		const competitionStatus = filterBar?.querySelector('[data-competition-filter-status]');
		const competitionLabel = filterBar?.querySelector('[data-competition-filter-label]');
		const judgeButtons = [...matrix.querySelectorAll('[data-judge-filter]')];
		const competitionButtons = [...matrix.querySelectorAll('[data-competition-filter]')];
		const sortButtons = [...document.querySelectorAll('[data-matrix-sort]')];
		const detailButtons = [...matrix.querySelectorAll('.cell-detail-button')];
		const detailPopover = document.querySelector('[data-cell-detail-popover]');
		const detailContent = detailPopover?.querySelector('[data-cell-detail-content]');
		let activeJudgeRow = null;
		let activeCompetitionId = null;
		let activeSort = 'overall';
		let pinnedDetailButton = null;
		let openDetailButton = null;
		let detailTimer = null;

		const positionDetail = (button) => {
			if (window.matchMedia('(max-width: 800px)').matches) {
				detailPopover.style.removeProperty('left');
				detailPopover.style.removeProperty('top');
				return;
			}

			const anchor = button.getBoundingClientRect();
			const popover = detailPopover.getBoundingClientRect();
			const left = Math.min(Math.max(12, anchor.left), window.innerWidth - popover.width - 12);
			const below = anchor.bottom + 8;
			const top = below + popover.height <= window.innerHeight - 12
				? below
				: Math.max(12, anchor.top - popover.height - 8);
			detailPopover.style.left = `${left}px`;
			detailPopover.style.top = `${top}px`;
		};

		const closeDetail = () => {
			clearTimeout(detailTimer);
			openDetailButton?.setAttribute('aria-expanded', 'false');
			detailPopover.hidden = true;
			detailContent.replaceChildren();
			openDetailButton = null;
			pinnedDetailButton = null;
		};

		const showDetail = (button) => {
			const template = button.parentElement.querySelector('[data-cell-detail-template]');
			if (!template) return;
			openDetailButton?.setAttribute('aria-expanded', 'false');
			detailContent.replaceChildren(template.content.cloneNode(true));
			detailPopover.hidden = false;
			openDetailButton = button;
			button.setAttribute('aria-expanded', 'true');
			positionDetail(button);
		};

		const scheduleDetailClose = () => {
			clearTimeout(detailTimer);
			if (!pinnedDetailButton) detailTimer = setTimeout(closeDetail, 120);
		};

		const setScoreClass = (cell, value, eligible) => {
			cell.classList.remove('positive', 'negative', 'neutral');
			if (!eligible) return;
			cell.classList.add(value > 50 ? 'positive' : value < 50 ? 'negative' : 'neutral');
		};

		const sortRows = (valueForRow) => {
			rows
				.sort((left, right) => valueForRow(right) - valueForRow(left) ||
					(activeSort === 'final' ? dataValue(right, 'overallFDeviation') - dataValue(left, 'overallFDeviation') : 0) ||
					left.querySelector('strong').textContent.localeCompare(right.querySelector('strong').textContent))
				.forEach((row) => body.appendChild(row));
		};

		const dataValue = (row, key) => row.dataset[key] === '' || row.dataset[key] === undefined
			? Number.NEGATIVE_INFINITY
			: Number(row.dataset[key]);

		const sortValue = (row) => ({
			overall: dataValue(row, 'overallSupport'),
			final: dataValue(row, 'overallF'),
			preliminary: dataValue(row, 'overallP'),
			coverage: dataValue(row, 'coverage')
		})[activeSort];

		const competitionCell = (row, competitionId) => [...row.querySelectorAll('[data-competition-id]')]
			.find((cell) => cell.dataset.competitionId === competitionId);
		const competitionScore = (cell) => {
			if (!cell) return Number.NEGATIVE_INFINITY;
			const finalSupport = Number(cell.dataset.finalSupport);
			if (cell.dataset.finalSupport !== '' && Number.isFinite(finalSupport)) return finalSupport;
			const preliminarySupport = Number(cell.dataset.preliminarySupport);
			return cell.dataset.preliminarySupport !== '' && Number.isFinite(preliminarySupport)
				? preliminarySupport
				: Number.NEGATIVE_INFINITY;
		};

		const applyFilters = () => {
			matrix.querySelectorAll('[data-competition-id]').forEach((cell) => {
				const hasJudgeValue = !activeJudgeRow || Number.isFinite(competitionScore(competitionCell(activeJudgeRow, cell.dataset.competitionId)));
				cell.hidden = activeCompetitionId
					? cell.dataset.competitionId !== activeCompetitionId
					: !hasJudgeValue;
			});

			rows.forEach((row) => {
				const selectedCompetitionCell = activeCompetitionId ? competitionCell(row, activeCompetitionId) : null;
				row.hidden = (activeJudgeRow && row !== activeJudgeRow) ||
					(activeCompetitionId && !Number.isFinite(competitionScore(selectedCompetitionCell)));
				const summaryCell = row.querySelector('[data-summary-cell]');
				const value = activeCompetitionId ? competitionScore(selectedCompetitionCell) : dataValue(row, 'overall');
				summaryCell.textContent = activeCompetitionId
					? selectedCompetitionCell?.querySelector('.cell-detail-button')?.textContent
					: summaryCell.dataset.overallText;
				if (activeCompetitionId) setScoreClass(summaryCell, value, row.dataset.eligible === 'true' && Number.isFinite(value));
				else summaryCell.classList.remove('positive', 'negative', 'neutral');
			});

			summaryHeading.textContent = activeCompetitionId ? 'Competition value' : 'Overall support';
			judgeStatus.hidden = !activeJudgeRow;
			competitionStatus.hidden = !activeCompetitionId;
			filterBar.hidden = !activeJudgeRow && !activeCompetitionId;
			sortRows(activeCompetitionId
				? (row) => competitionScore(competitionCell(row, activeCompetitionId))
				: sortValue);
		};

		const selectCompetition = (button) => {
			const competitionId = button.dataset.competitionFilter;
			activeCompetitionId = activeCompetitionId === competitionId ? null : competitionId;
			competitionButtons.forEach((candidate) => candidate.setAttribute('aria-pressed', String(candidate.dataset.competitionFilter === activeCompetitionId)));
			competitionLabel.textContent = activeCompetitionId ? button.dataset.filterLabel : '';
			applyFilters();
		};

		const selectJudge = (button) => {
			const row = button.closest('[data-judge-row]');
			activeJudgeRow = activeJudgeRow === row ? null : row;
			judgeButtons.forEach((candidate) => candidate.setAttribute('aria-pressed', String(candidate.closest('[data-judge-row]') === activeJudgeRow)));
			judgeLabel.textContent = activeJudgeRow ? button.dataset.judgeFilter : '';
			applyFilters();
		};

		competitionButtons.forEach((button) => button.addEventListener('click', () => selectCompetition(button)));
		judgeButtons.forEach((button) => button.addEventListener('click', () => selectJudge(button)));
		sortButtons.forEach((button) => button.addEventListener('click', () => {
			activeSort = button.dataset.matrixSort;
			sortButtons.forEach((candidate) => candidate.setAttribute('aria-pressed', String(candidate === button)));
			applyFilters();
		}));
		detailButtons.forEach((button) => {
			button.addEventListener('pointerenter', () => {
				clearTimeout(detailTimer);
				if (!pinnedDetailButton) detailTimer = setTimeout(() => showDetail(button), 180);
			});
			button.addEventListener('pointerleave', scheduleDetailClose);
			button.addEventListener('focus', () => { if (!pinnedDetailButton) showDetail(button); });
			button.addEventListener('blur', scheduleDetailClose);
			button.addEventListener('click', () => {
				if (pinnedDetailButton === button) {
					closeDetail();
					return;
				}
				pinnedDetailButton = button;
				showDetail(button);
			});
		});
		detailPopover?.addEventListener('pointerenter', () => clearTimeout(detailTimer));
		detailPopover?.addEventListener('pointerleave', scheduleDetailClose);
		detailPopover?.querySelector('[data-cell-detail-close]')?.addEventListener('click', closeDetail);
		document.addEventListener('pointerdown', (event) => {
			if (pinnedDetailButton && !detailPopover.contains(event.target) && !pinnedDetailButton.contains(event.target)) closeDetail();
		});
		document.addEventListener('keydown', (event) => { if (event.key === 'Escape' && openDetailButton) closeDetail(); });
		window.addEventListener('resize', () => { if (openDetailButton) positionDetail(openDetailButton); });
		filterBar?.querySelector('[data-competition-filter-clear]')?.addEventListener('click', () => {
			activeCompetitionId = null;
			competitionButtons.forEach((button) => button.setAttribute('aria-pressed', 'false'));
			applyFilters();
		});
		filterBar?.querySelector('[data-judge-filter-clear]')?.addEventListener('click', () => {
			activeJudgeRow = null;
			judgeButtons.forEach((button) => button.setAttribute('aria-pressed', 'false'));
			applyFilters();
		});
		applyFilters();
	}
